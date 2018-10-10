using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Events;

namespace ZennMusic
{
    internal static class ChatManager
    {
        private static readonly TwitchClient client = new TwitchClient();
        private static bool connection;
        private static string[] ManagerNameList = { "producerzenn" };

        public static void InitializeChatManager()
        {
            const string botId = "shiftbot1124";
            const string botToken = "aj8ah4035083xw27tg3d1cuon5yjnj";

            var credentials = new ConnectionCredentials(botId, botToken);

            client.Initialize(credentials, "producerzenn");

            client.OnError += (sender, e) => Console.WriteLine($@"[ERROR] {e.Exception}");
            client.OnMessageReceived += OnMessageReceived;

            client.Connect();
        }

        private static void OnMessageReceived(object sender, OnMessageReceivedArgs args)
        {
            if (args.ChatMessage.Message.Split()[0] == "=젠")
            {
                var commandArgs = args.ChatMessage.Message.Split().Skip(1).ToArray();

                switch (commandArgs[0])
                {
                    case "테스트":
                        client.SendMessage(args.ChatMessage.Channel, "테스트!");
                        break;
                    case "조각":
                        var pieceData = SheetManager.Sheet;
                        var search = pieceData
                            .FirstOrDefault(x => (x[0] as string)?.Replace(" ", "") == args.ChatMessage.DisplayName);

                        if (search is null)
                        {
                            client.SendMessage(args.ChatMessage.Channel, "신청곡 조각 시트에 이름이 존재하지 않아요!");
                        }
                        else
                        {
                            client.SendMessage(args.ChatMessage.Channel,
                                $"{args.ChatMessage.DisplayName}님의 신청곡 조각 : " +
                                $"{(search.Count > 1 ? (search[1] ?? 0) : 0)} / 신청곡 : {(search.Count > 2 ? (search[2] ?? 0) : 0)}" +
                                $"{((search.Count > 3 && (search[3] as string) != string.Empty) ? (" (" + search[3] + ")") : string.Empty)}");
                        }

                        break;
                    case "지급":

                        const string spreadSheetId = "1fndP3ddyqehCIn6vcpEiZOOixzYN6MX8puCnLdOIqgM";

                        pieceData = SheetManager.Sheet;
                        search = pieceData
                            .FirstOrDefault(x => (x[0] as string)?.Replace(" ", "") == args.ChatMessage.DisplayName);

                        if (search is null)
                        {
                            client.SendMessage(args.ChatMessage.Channel, "신청곡 조각 시트에 이름이 존재하지 않아요!");
                        }

                        int type = 0;
                        switch (commandArgs[1])
                        {
                            case "조각":
                                type = 1;
                                break;
                            case "곡":
                                type = 2;
                                break;
                            default:
                                client.SendMessage(args.ChatMessage.Channel, "잘못된 명령어 형식입니다. \"=젠 지급 (조각/곡) 닉네임\"의 형식으로 입력해주세요.");
                                return;
                        }

                        var range = $"시트1!B{pieceData.ToList().FindIndex(x => x[0] as string == commandArgs[2]) + 6}";

                        search[type] = int.Parse(search[type] as string ?? "0") + 1;

                        var body = new ValueRange();
                        body.Values = new List<IList<object>> { new List<object> { null, search[1], search[2] } };

                        var req = SheetManager.Service.Spreadsheets.Values.Update(body, spreadSheetId, range);
                        req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                        req.Execute();

                        break;
                }
            }
        }
    }
}