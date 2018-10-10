using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZennMusic
{
    internal static class SheetManager
    {
        private static readonly string[] Scopes = { SheetsService.Scope.Spreadsheets };

        public static SheetsService Service { get; private set; }
        public static IList<IList<object>> Sheet => LoadSheet();

        private static void InitService()
        {
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
                HttpClientInitializer = credential
            });
        }

        private static IList<IList<object>> LoadSheet()
        {
            if(Service is null)
                InitService();

            const string spreadSheetId = "1fndP3ddyqehCIn6vcpEiZOOixzYN6MX8puCnLdOIqgM";
            const string range = "시트1!B6:E";

            var req = Service.Spreadsheets.Values.Get(spreadSheetId, range);

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

            return res;
        }
    }
}
