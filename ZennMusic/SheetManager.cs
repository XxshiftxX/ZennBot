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
    static class SheetManager
    {
        private readonly static string[] Scopes = { SheetsService.Scope.SpreadsheetsReadonly };

        public static IList<IList<object>> LoadSheet()
        {
            UserCredential credential;

            using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                string credentialPath = "token.json";

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets, // ClientSecrets
                    Scopes, // Scopes
                    "ZENNBOT", // User
                    CancellationToken.None, // TaskCancellationToken
                    new FileDataStore(credentialPath, true) // DataStore
                    ).Result;

                var service = new SheetsService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential
                });

                var spreadSheetId = "1XuWOrZ1rA-7O5RAFKvJ__wIue4u_WTRyyOZpFXIP7Ko";
                var range = "시트1!B6:E";

                var req = service.Spreadsheets.Values.Get(spreadSheetId, range);

                var res = req.Execute().Values;

                foreach (var row in res)
                {
                    for (int i = 1; i < 4; i++)
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
}
