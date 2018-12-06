using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace ZennMusic
{
    internal static class SheetManager
    {
        private static readonly string[] Scopes = { SheetsService.Scope.Spreadsheets };

        public static string SpreadSheetId = "1XuWOrZ1rA-7O5RAFKvJ__wIue4u_WTRyyOZpFXIP7Ko";
        public static SheetsService Service { get; private set; }
        public static IList<IList<object>> PieceSheet => LoadPieceSheet();
        public static IList<object> NullNameSheet => LoadNullNameSheet();

        public static void InitializeSheet()
        {
            LogManager.Log("[Sheet System Initialize] Start");
            UserCredential credential;

            using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                const string credentialPath = "token.json";

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets, // ClientSecrets
                    Scopes, // Scopes
                    "ZENNBOT", // User
                    CancellationToken.None, // TaskCancellationToken
                    new FileDataStore(credentialPath, true) // DataStore
                ).Result;
            }

            Service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Zenn Bot"
            });
            LogManager.Log("[Sheet System Initialize] Complete");
        }

        private static IList<IList<object>> LoadPieceSheet()
        {
            LogManager.Log("[Load Sheet] Start");

            if(Service is null)
                InitializeSheet();

            const string range = "시트1!B6:E";

            LogManager.Log("[Load Sheet] Google docs api execute");
            var req = Service.Spreadsheets.Values.Get(SpreadSheetId, range);
            var res = req.Execute().Values;

            foreach (var row in res)
            {
                for (var i = 1; i < 4; i++)
                {
                    if (row.Count <= i)
                        row.Add(i == 3 ? (object)string.Empty : 0);
                    else if (row[i] == null)
                        row[i] = 0;
                }
            }

            LogManager.Log("[Load Sheet] Complete");
            return res;
        }

        private static IList<object> LoadNullNameSheet()
        {
            LogManager.Log("[Load Nullname Sheet] Start");

            if (Service is null)
                InitializeSheet();

            const string range = "시트1!J6:J";

            LogManager.Log("[Load Nullname Sheet] Google docs api execute");
            var req = Service.Spreadsheets.Values.Get(SpreadSheetId, range);

            var res = req.Execute().Values ?? new List<IList<object>>();
            var finalRes = res.Select(x => x.Count > 0 ? x[0] : string.Empty).ToList();

            LogManager.Log("[Load Nullname Sheet] Complete");
            return finalRes;
        }
    }
}
