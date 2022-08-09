using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Jackett.Common.Indexers.Abstract;
using Jackett.Common.Models;
using Jackett.Common.Services.Interfaces;
using Jackett.Common.Utils;
using Newtonsoft.Json.Linq;
using NLog;
using WebClient = Jackett.Common.Utils.Clients.WebClient;

namespace Jackett.Common.Indexers
{
    [ExcludeFromCodeCoverage]
    public class GreatPosterWall : GazelleTracker
    {
        public GreatPosterWall(IIndexerConfigurationService configService, WebClient wc, Logger l, IProtectionService ps,
            ICacheService cs)
            : base(id: "greatposterwall",
                   name: "GreatPosterWall",
                   description: "GreatPosterWall (GPW) is a CHINESE Private site for MOVIES",
                   link: "https://greatposterwall.com/",
                   caps: new TorznabCapabilities
                   {
                       MovieSearchParams = new List<MovieSearchParam>
                       {
                           MovieSearchParam.Q, MovieSearchParam.ImdbId, MovieSearchParam.Genre
                       }
                   },
                   configService: configService,
                   client: wc,
                   logger: l,
                   p: ps,
                   cs: cs,
                   supportsFreeleechTokens: true,
                   imdbInTags: false,
                   has2Fa: true,
                   useApiKey: false,
                   usePassKey: false,
                   instructionMessageOptional: null
                  )
        {
            Language = "zh-CN";
            Type = "private";

            AddCategoryMapping(1, TorznabCatType.Movies, "Movies 电影");
        }

        protected override async Task<IEnumerable<ReleaseInfo>> PerformQuery(TorznabQuery query)
        {
            // GPW uses imdbid in the searchstr so prevent cataloguenumber or taglist search.
            if (query.IsImdbQuery)
            {
                query.SearchTerm = query.ImdbID;
                query.ImdbID = null;
            }

            return await base.PerformQuery(query);
        }

        protected override bool ReleaseInfoPostParse(ReleaseInfo release, JObject torrent, JObject result)
        {
            // override release title
            var groupName = WebUtility.HtmlDecode((string)result["groupName"]);
            var groupSubName = WebUtility.HtmlDecode((string)result["groupSubName"]);
            var groupYear = (string)result["groupYear"];
            var title = new StringBuilder();
            title.Append(groupName);
            if (!string.IsNullOrEmpty(groupSubName))
                title.Append(" " + groupSubName + " ");

            if (!string.IsNullOrEmpty(groupYear) && groupYear != "0")
                title.Append(" [" + groupYear + "]");


            var torrentId = torrent["torrentId"];

            var flags = new List<string>();

            var codec = (string)torrent["codec"];
            if (!string.IsNullOrEmpty(codec))
                flags.Add(codec);

            var source = (string)torrent["source"];
            if (!string.IsNullOrEmpty(source))
                flags.Add(source);

            var resolution = (string)torrent["resolution"];
            if (!string.IsNullOrEmpty(resolution))
                flags.Add(resolution);

            var container = (string)torrent["container"];
            if (!string.IsNullOrEmpty(container))
                flags.Add(container);

            var processing = (string)torrent["processing"];
            if (!string.IsNullOrEmpty(processing))
                flags.Add(processing);

            if (flags.Count > 0)
                title.Append(" " + string.Join(" / ", flags));

            release.Title = title.ToString();
            switch ((string)torrent["freeType"])
            {
                case "11":
                    release.DownloadVolumeFactor = 0.75;
                    break;
                case "12":
                    release.DownloadVolumeFactor = 0.5;
                    break;
                case "13":
                    release.DownloadVolumeFactor = 0.25;
                    break;
                case "1":
                    release.DownloadVolumeFactor = 0;
                    break;
                case "2":
                    release.DownloadVolumeFactor = 0;
                    release.UploadVolumeFactor = 0;
                    break;
            }

            var isPersonalFreeleech = (bool?)torrent["isPersonalFreeleech"];
            if (isPersonalFreeleech != null && isPersonalFreeleech == true)
                release.DownloadVolumeFactor = 0;

            var imdbID = (string)result["imdbId"];
            if (!string.IsNullOrEmpty(imdbID))
                release.Imdb = ParseUtil.GetImdbID(imdbID);

            release.MinimumRatio = 1;
            release.MinimumSeedTime = 172800; // 48 hours
                                              // tag each results with Movie cats.
            release.Category = new List<int> { TorznabCatType.Movies.ID };
            return true;
        }
    }
}
