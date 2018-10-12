using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
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
        private static string[] ManagerNameList = { "producerzenn", "qjfrntop" };
        private static readonly Dictionary<string, Action<OnMessageReceivedArgs ,string[]>> Commands = 
            new Dictionary<string, Action<OnMessageReceivedArgs, string[]>>();
        public static readonly List<SongRequest> SongList = new List<SongRequest>();
        public static bool IsEditingSongList = false;
        public static bool IsRefreshingSongList = false;

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

        public static void InitializeCommand()
        {
            Commands["테스트"] = (arg, cmdarg) => client.SendMessage(arg.ChatMessage.Channel, "테스트!");
            Commands["조각"] = GetPiece;
            Commands["지급"] = PayPiece;
            Commands["신청"] = RequestSong;
        }

        private static void GetPiece(OnMessageReceivedArgs args, string[] commandArgs)
        {
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
        }

        private static void PayPiece(OnMessageReceivedArgs args, string[] commandArgs)
        {
            if (!ManagerNameList.Contains(args.ChatMessage.Username))
            {
                client.SendMessage(args.ChatMessage.Channel, "권한이 없습니다!");
                return;
            }

            if (commandArgs.Length < 4)
            {
                client.SendMessage(args.ChatMessage.Channel, "잘못된 명령어 형식입니다. \"=젠 지급 (조각/곡) 닉네임\"의 형식으로 입력해주세요.");
                return;
            }

            var pieceData = SheetManager.Sheet;
            var search = pieceData
                .FirstOrDefault(x => (x[0] as string)?.Replace(" ", "") == commandArgs[2]);

            if (search is null)
            {
                client.SendMessage(args.ChatMessage.Channel, "신청곡 조각 시트에 이름이 존재하지 않아요!");
                return;
            }

            var type = 0;
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

            var body = new ValueRange
            {
                Values = new List<IList<object>>
                {
                    new List<object> {null, search[1], search[2]}
                }
            };

            var req = SheetManager.Service.Spreadsheets.Values.Update(body, SheetManager.SpreadSheetId, range);
            req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            req.Execute();

            client.SendMessage(args.ChatMessage.Channel, $"{commandArgs[2]}님께 {(type == 1 ? "조각" : "신청곡")} 1개가 지급되었습니다.");
        }

        private static void RequestSong(OnMessageReceivedArgs args, string[] commandArgs)
        { 
            var pieceData = SheetManager.Sheet;
            var search = pieceData
                .FirstOrDefault(x => (x[0] as string)?.Replace(" ", "") == args.ChatMessage.DisplayName);
            var song = string.Join(" ", commandArgs.Skip(1));

            if (search is null)
            {
                client.SendMessage(args.ChatMessage.Channel, "신청곡 조각 시트에 이름이 존재하지 않아요!");
                return;
            }

            if (song.Replace(" ", string.Empty) == string.Empty)
            {
                client.SendMessage(args.ChatMessage.Channel, "신청곡의 이름을 입력해주세요!");
                return;
            }

            var songCount = 0;
            var piece = 0;

            if (search.Count > 1)
                songCount = int.Parse(search[2] as string ?? "0");
            if (search.Count > 2)
                piece = int.Parse(search[1] as string ?? "0");

            var body = new ValueRange
            {
                Values = new List<IList<object>>
                {
                    new List<object> {null, piece, songCount}
                }
            }; ;

            SongRequestPayment reqPayment;
            if (songCount > 0)
            {
                body.Values[0][2] = (int)body.Values[0][2] - 1;
                client.SendMessage(args.ChatMessage.Channel, $"티켓 한장을 소모하여 신청곡을 신청했어요! (곡명 : {song})");

                reqPayment = SongRequestPayment.Ticket;

            }
            else if (piece > 2)
            {
                body.Values[0][1] = (int)body.Values[0][1] - 3;
                client.SendMessage(args.ChatMessage.Channel, $"신청곡 조각 3개를 소모하여 신청곡을 신청했어요! (곡명 : {song})");

                reqPayment = SongRequestPayment.Piece;
            }
            else
            {
                client.SendMessage(args.ChatMessage.Channel, "조각이나 티켓이 부족합니다! =젠 조각 명령어로 보유 조각을 확인해주세요!");
                return;
            }

            var range = $"시트1!B{pieceData.ToList().FindIndex(x => x[0] as string == args.ChatMessage.DisplayName) + 6}";

            var req = SheetManager.Service.Spreadsheets.Values.Update(body, SheetManager.SpreadSheetId, range);
            req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            req.Execute();

            SongList.Add(new SongRequest(song, args.ChatMessage.DisplayName, reqPayment));
        }

        private static void OnMessageReceived(object sender, OnMessageReceivedArgs args)
        {
            if (args.ChatMessage.Message.Split()[0] != "=젠") return;

            var commandArgs = args.ChatMessage.Message.Split().Skip(1).ToArray();

            if (!Commands.ContainsKey(commandArgs[0]))
                client.SendMessage(args.ChatMessage.Channel, "존재하지 않는 명령어입니다!");
            else
                Commands[commandArgs[0]](args, commandArgs);
        }
    }
}