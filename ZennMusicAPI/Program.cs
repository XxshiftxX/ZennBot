using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;
using Ofl.Twitch;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;

namespace ZennMusicAPI
{
    class Program
    {
        
        static void Main(string[] args)
        {
            var sheets = new Sheets();
            var bot = new Bot(sheets.res);
        }
    }

    class Sheets
    {
        string sheetID = "1XuWOrZ1rA-7O5RAFKvJ__wIue4u_WTRyyOZpFXIP7Ko";
        public IList<IList<object>> res;
        public Sheets()
        {
            string[] Scopes = { SheetsService.Scope.SpreadsheetsReadonly };
            var ApplicationName = "Google Sheets API .NET Quickstart";

            UserCredential credential;

            using (var stream =
                new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            // Create Google Sheets API service.
            var service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            // Define request parameters.
            String spreadsheetId = "1XuWOrZ1rA-7O5RAFKvJ__wIue4u_WTRyyOZpFXIP7Ko";
            String range = "시트1!B6:E";
            SpreadsheetsResource.ValuesResource.GetRequest request =
                    service.Spreadsheets.Values.Get(spreadsheetId, range);

            // Prints the names and majors of students in a sample spreadsheet:
            // https://docs.google.com/spreadsheets/d/1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms/edit
            ValueRange response = request.Execute();
            IList<IList<Object>> values = response.Values;
            if (values != null && values.Count > 0)
            {
                Console.WriteLine(@"Name, Major");
                foreach (var row in values)
                {
                    // Print columns A and E, which correspond to indices 0 and 4.
                    Console.WriteLine(string.Join(" / ", row));
                }
                res = values;
            }
            else
            {
                Console.WriteLine(@"No data found.");
            }
        }
    }

    class Bot
    {
        TwitchClient client;
        IList<IList<object>> chart;

        public Bot()
        {
            Bott(null);
        }

        public Bot(IList<IList<object>> chart)
        {
            Bott(chart);
        }

        public void Bott(IList<IList<object>> chart)
        {
            ConnectionCredentials credentials = new ConnectionCredentials("shiftbot1124", "aj8ah4035083xw27tg3d1cuon5yjnj");

            client = new TwitchClient();
            client.Initialize(credentials, "producerzenn");

            client.OnConnected += OnConnected;
            client.OnError += (sender, e) => Console.WriteLine(e.Exception);
            client.OnJoinedChannel += OnJoinedChannel;
            client.OnMessageReceived += OnMessageReceived;
            client.OnWhisperReceived += OnWhisperReceived;
            //client.OnLog += (sender, e) => Console.WriteLine(e.Data);

            this.chart = chart;

            client.Connect();

            Console.ReadLine();
        }

        private void OnConnected(object sender, OnConnectedArgs e)
        {
            Console.WriteLine($@"Connected to {e.AutoJoinChannel}");
        }
        private void OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            Console.WriteLine(@"Hey guys! I am a bot connected via TwitchLib!");
            //client.SendMessage(e.Channel, "Hey guys! I am a bot connected via TwitchLib!");
        }

        private void OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            Console.WriteLine($@"{e.ChatMessage.DisplayName}");
            Console.WriteLine($@"{e.ChatMessage.Message}");

            if (e.ChatMessage.DisplayName == "Twipkr" && e.ChatMessage.Message.Contains("플래티넘 신청곡 티켓"))
            {
                client.SendMessage(e.ChatMessage.Channel, $"{e.ChatMessage.Message}님... 머시써... 도네... 쩌러....");
            }

            if (e.ChatMessage.Message.StartsWith("=젠 "))
            {
                var args = e.ChatMessage.Message.Split().Skip(1).ToArray();
                Console.WriteLine($@"명령어! : {args[0]}");
                if (args[0] == "테스트")
                    client.SendMessage(e.ChatMessage.Channel, "테스트!");

                if (args[0] == "조각")
                {
                    var search = chart.Where(x => (x[0] as string).Replace(" ", "") == e.ChatMessage.DisplayName.Replace(" ", "")).FirstOrDefault();

                    if (search == null)
                    {
                        client.SendMessage(e.ChatMessage.Channel, "신청곡 조각 시트에 이름이 존재하지 않아요!");
                    }
                    else
                    {
                        client.SendMessage(e.ChatMessage.Channel, $"{e.ChatMessage.DisplayName}님의 신청곡 조각 : " +
                            $"{(search.Count > 1 ? (search[1] ?? 0) : 0)} / 신청곡 : {(search.Count > 2 ? (search[2] ?? 0) : 0)}" +
                            $"{(search.Count > 3 ? (" (" + search[3] + ")") : string.Empty)}");
                    }
                }

            }

            Console.WriteLine($@"----");

        }

        private void OnWhisperReceived(object sender, OnWhisperReceivedArgs e)
        {
            if (e.WhisperMessage.Username == "my_friend")
                client.SendWhisper(e.WhisperMessage.Username, "Hey! Whispers are so cool!!");
        }
    }
}
