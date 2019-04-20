using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Newtonsoft.Json.Linq;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace ZennMusic
{
    internal static class ChatManager
    {
        private static readonly TwitchClient client = new TwitchClient();
        private static readonly TwitchAPI api = new TwitchAPI();
        private static string[] ManagerNameList = { "producerzenn", "qjfrntop", "freewing8101", "flashman0509", "mohamgwa1" };
        private static readonly Dictionary<string, Action<OnMessageReceivedArgs, string[]>> Commands =
            new Dictionary<string, Action<OnMessageReceivedArgs, string[]>>();
        public static readonly ObservableCollection<SongRequest> SongList = new ObservableCollection<SongRequest>();
        public static readonly List<SongRequest> DeletedSongList = new List<SongRequest>();
        public static bool IsRequestAvailable = false;

        public static List<(string name, int tier)> AttendanceList;

        public static void InitializeChatManager()
        {
            var currentPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) ?? "D:\\";
            var filePath = Path.Combine(currentPath, "config.json");
            var config = JObject.Parse(File.ReadAllText(filePath));

            var botId = config["BotId"].Value<string>();
            var botToken = config["BotToken"].Value<string>();

            api.Settings.ClientId = config["ClientId"].Value<string>();
            api.Settings.AccessToken = config["AccessToken"].Value<string>();
            api.Settings.Secret = config["Secret"].Value<string>();

            ManagerNameList = config["Managers"].Value<JArray>().Select(x => x.Value<string>()).ToArray();

            var credentials = new ConnectionCredentials(botId, botToken);

            client.Initialize(credentials, "producerzenn");

            client.OnError += (sender, e) => Console.WriteLine($@"[ERROR] {e.Exception}");
            client.OnMessageReceived += OnMessageReceived;

            client.OnGiftedSubscription += (e, arg) => client.SendMessage(arg.Channel, "저격이다! 읖드려!!!");

            client.Connect();
        }

        public static void InitializeCommand()
        {
            LogManager.Log("[Command System Initialize] Start");
            Commands["조각"] = GetPiece;
            Commands["지급"] = PayPiece;
            Commands["신청"] = RequestSong;
            Commands["곡"] = ChackSong;

            Commands["출석"] = CheckAttendance;
            LogManager.Log("[Command System Initialize] Complete");
        }

        public static void OnMessageReceived(object sender, OnMessageReceivedArgs args)
        {
            LogManager.Log($"[Chat Event] ({args.ChatMessage.DisplayName}) {args.ChatMessage.Message}");
            if (args.ChatMessage.Message.Split()[0] == "!아이돌")
            {
                var commandArgs = args.ChatMessage.Message.Split().Skip(1).ToArray();

                GetIdolInfo(args, commandArgs);
            }
            else if (args.ChatMessage.Message.Split()[0] == "=젠")
            {
                LogManager.Log("[Chat Event] Command prefix detected");

                var commandArgs = args.ChatMessage.Message.Split().Skip(1).ToArray();

                if (!Commands.ContainsKey(commandArgs[0]))
                {
                    LogManager.Log("[Chat Event] Unavailable command");
                    client.SendMessage(args.ChatMessage.Channel, "존재하지 않는 명령어입니다!");
                }
                else
                {
                    LogManager.Log("[Chat Event] Command execute");
                    Application.Current.Dispatcher.Invoke(() => Commands[commandArgs[0]](args, commandArgs));
                }
            }
            else if (AttendanceList != null)
            {
                lock (AttendanceList)
                {
                    var tier = 0;

                    if (args.ChatMessage.EmoteSet.Emotes.Exists(x => x.Name == "produc1Tricol"))
                        tier = 3;
                    else if (args.ChatMessage.EmoteSet.Emotes.Exists(x => x.Name == "produc1Gold"))
                        tier = 2;
                    else if (args.ChatMessage.EmoteSet.Emotes.Exists(x => x.Name == "produc1Ffffff"))
                    {
                        tier = 1;
                    }

                    if (tier == 0)
                        return;

                    LogManager.Log("[Attendance] Emote detected");
                    AttendanceList.Add((args.ChatMessage.DisplayName, tier));
                    LogManager.Log("[Attendance] Complete");
                }
            }
        }

        private static void ProcessNullName(OnMessageReceivedArgs args, string funcType, bool doLog = true)
        {
            LogManager.Log("[Null Name] Null name process started");

            if (doLog)
                client.SendMessage(args.ChatMessage.Channel, "신청곡 조각 시트에 이름이 존재하지 않아요!");

            var nullData = SheetManager.NullNameSheet;

            nullData.Add(funcType);

            var nullBody = new ValueRange
            {
                Values = nullData.Select(x => new List<object> { x } as IList<object>).ToList()
            };

            var nullRequest = SheetManager.Service.Spreadsheets.Values.Update(nullBody, SheetManager.PieceSpreadSheetId,
                "시트1!J6");
            nullRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            LogManager.Log("[Null Name] Google docs api request executed");
            nullRequest.Execute();
        }

        // =젠 조각(0)
        private static void GetPiece(OnMessageReceivedArgs args, string[] commandArgs)
        {
            LogManager.Log("[Check Piece] Command started");
            
            var pieceData = SheetManager.PieceSheet;
            var search = pieceData
                .FirstOrDefault(x => (x[0] as string)?.Replace(" ", "") == args.ChatMessage.DisplayName);

            if (search is null)
            {
                LogManager.Log($"[Check Piece] Unknown user : {args.ChatMessage.DisplayName}");
                ProcessNullName(args, $"{args.ChatMessage.DisplayName}(조각 조회)");
            }
            else
            {
                client.SendMessage(args.ChatMessage.Channel,
                    $"{args.ChatMessage.DisplayName}님의 신청곡 조각 : " +
                    $"{(search.Count > 1 ? (search[1] ?? 0) : 0)} / 신청곡 티켓 : {(search.Count > 2 ? (search[2] ?? 0) : 0)}" +
                    $"{((search.Count > 3 && (search[3] as string) != string.Empty) ? (" (" + search[3] + ")") : string.Empty)}");
            }

            LogManager.Log("[Check Piece] Complete");
        }

        // =젠 지급(0) 곡(1) 시프트(2) 020(3)
        private static void PayPiece(OnMessageReceivedArgs args, string[] commandArgs)
        {
            LogManager.Log("[Pay Piece] Command started");
            var PieceCount = 1;

            if (!ManagerNameList.Contains(args.ChatMessage.Username))
            {
                LogManager.Log("[Pay Piece] No permission");
                client.SendMessage(args.ChatMessage.Channel, "권한이 없습니다!");
                return;
            }
            if (commandArgs.Length < 3 || !(commandArgs[1] == "곡" || commandArgs[1] == "조각"))
            {
                LogManager.Log("[Pay Piece] Unavailable command form");
                client.SendMessage(args.ChatMessage.Channel, "잘못된 명령어 형식입니다. \"=젠 지급 (조각/곡) 닉네임 [갯수]\"의 형식으로 입력해주세요.");
                return;
            }
            if (commandArgs.Length >= 4)
            {
                if (!int.TryParse(commandArgs[3], out PieceCount))
                {
                    LogManager.Log("[Pay Piece] No integer in arg-3");
                    client.SendMessage(args.ChatMessage.Channel, "갯수 입력 부분에는 숫자를 입력해주세요!");
                    return;
                }
                if (PieceCount < 1)
                {
                    LogManager.Log("[Pay Piece] Negative number in arg-3");
                    client.SendMessage(args.ChatMessage.Channel, "왜 남의 조각을 뺏어가려고 하세요? ㅡㅡ 수량은 1 이상으로 입력하세요!");
                    return;
                }
            }

            var pieceData = SheetManager.PieceSheet;
            var search = pieceData
                .FirstOrDefault(x => (x[0] as string)?.Replace(" ", "") == commandArgs[2]);

            if (search is null)
            {
                LogManager.Log($"[Pay Piece] Unknown user : {commandArgs[2]}");
                ProcessNullName(args, $"{commandArgs[2]}(지급-{commandArgs[1]})");
                return;
            }

            int type;
            switch (commandArgs[1])
            {
                case "조각":
                    type = 1;
                    break;
                case "곡":
                    type = 2;
                    break;
                default:
                    client.SendMessage(args.ChatMessage.Channel, 
                        "잘못된 명령어 형식입니다. \"=젠 지급 (조각/곡) 닉네임 [갯수]\"의 형식으로 입력해주세요.");
                    return;
            }

            var range = $"시트1!B{pieceData.ToList().FindIndex(x => x[0] as string == commandArgs[2]) + 6}";

            search[type] = int.Parse(search[type] as string ?? "0") + PieceCount;

            var body = new ValueRange
            {
                Values = new List<IList<object>>
                {
                    new List<object> { null, search[1], search[2] }
                }
            };

            var req = SheetManager.Service.Spreadsheets.Values.Update(body, SheetManager.PieceSpreadSheetId, range);
            req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            LogManager.Log("[Pay Piece] Google docs api request executed");
            req.Execute();

            client.SendMessage(args.ChatMessage.Channel, $"{commandArgs[2]}님께 {(type == 1 ? "조각" : "신청곡")} {PieceCount}개가 지급되었습니다.");
            LogManager.Log("[Pay Piece] Complete");
        }

        // =젠 신청(0) 인페르노 스퀘어링(1~)
        private static void RequestSong(OnMessageReceivedArgs args, string[] commandArgs)
        {
            LogManager.Log("[Request Song] Command started");

            if (!IsRequestAvailable)
            {
                LogManager.Log("[Request Song] Request disabled");
                client.SendMessage(args.ChatMessage.Channel, "현재 신청곡을 받고있지 않아요!");
                return;
            }
            if (commandArgs.Length < 2)
            {
                LogManager.Log("[Request Song] No song parameter");
                client.SendMessage(args.ChatMessage.Channel, "신청곡의 이름을 입력해주세요!");
                return;
            }

            var pieceData = SheetManager.PieceSheet;
            var search = pieceData
                .FirstOrDefault(x => (x[0] as string)?.Replace(" ", "") == args.ChatMessage.DisplayName);

            var song = string.Join(" ", commandArgs.Skip(1));

            if (search is null)
            {
                LogManager.Log($"[Request Song] Unknown user : {args.ChatMessage.DisplayName}");
                ProcessNullName(args, $"{args.ChatMessage.DisplayName}(신청곡)");
                return;
            }

            if (DeletedSongList.Concat(SongList).Reverse().Take(4).Any(x => x.UserName == args.ChatMessage.DisplayName))
            {
                LogManager.Log("[Request Song] Cooldown");
                client.SendMessage(args.ChatMessage.Channel,
                    "아직 쿨타임이에요! 이전에 신청한 곡과 현재 신청하는 곡 사이에 최소 4개의 곡이 있어야 해요!");
                return;
            }

            var songTicket = 0;
            var songPiece = 0;

            if (search.Count > 1)
                songTicket = int.Parse(search[2] as string ?? "0");
            if (search.Count > 2)
                songPiece = int.Parse(search[1] as string ?? "0");

            var body = new ValueRange
            {
                Values = new List<IList<object>>
                {
                    new List<object> {null, songPiece, songTicket}
                }
            };

            SongRequestPayment reqPayment;
            if (songTicket > 0)
            {
                body.Values[0][2] = (int)body.Values[0][2] - 1;
                client.SendMessage(args.ChatMessage.Channel, 
                    $"티켓 한장을 소모하여 신청곡을 신청했어요! (곡명 : {song})");

                reqPayment = SongRequestPayment.Ticket;

            }
            else if (songPiece > 2)
            {
                body.Values[0][1] = (int)body.Values[0][1] - 3;
                client.SendMessage(args.ChatMessage.Channel, 
                    $"신청곡 조각 3개를 소모하여 신청곡을 신청했어요! (곡명 : {song})");

                reqPayment = SongRequestPayment.Piece;
            }
            else
            {
                client.SendMessage(args.ChatMessage.Channel, 
                    "조각이나 티켓이 부족합니다! =젠 조각 명령어로 보유 조각을 확인해주세요!");
                return;
            }

            var range = $"시트1!B{pieceData.ToList().FindIndex(x => x[0] as string == args.ChatMessage.DisplayName) + 6}";

            var req = SheetManager.Service.Spreadsheets.Values.Update(body, SheetManager.PieceSpreadSheetId, range);
            req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            LogManager.Log("[Request Song] Google docs api executed");
            req.Execute();
            LogManager.Log("[Request Song] Google docs api execute");
            SongList.Add(new SongRequest(song, args.ChatMessage.DisplayName, reqPayment));
            LogManager.Log("[Request Song] Complete");
        }

        // =젠 출석(0)
        private static void CheckAttendance(OnMessageReceivedArgs arg, string[] cmdarg)
        {
            LogManager.Log("[Check Attendance] Command started");
            if (!ManagerNameList.Contains(arg.ChatMessage.Username))
            {
                LogManager.Log("[Check Attendance] No permission");
                client.SendMessage(arg.ChatMessage.Channel, "권한이 없습니다!");
                return;
            }

            if (AttendanceList == null)
            {
                LogManager.Log("[Check Attendance] Attendance mode enabled");
                client.SendMessage(arg.ChatMessage.Channel, "== 출석 체크가 시작되었습니다! ==");
                AttendanceList = new List<(string, int)>();
            }
            else
            {
                LogManager.Log("[Check Attendance] Attendance mode disabled");
                client.SendMessage(arg.ChatMessage.Channel, "== 출석 체크가 종료되었습니다! ==");

                var pieceData = SheetManager.PieceSheet;

                foreach (var (name, tier) in AttendanceList)
                {
                    var user = pieceData.FirstOrDefault(x => (x[0] as string) == name);
                    var count = tier;

                    if (user != null)
                        user[1] = int.Parse(user[1] as string ?? "0") + count;
                    else
                        ProcessNullName(arg, $"{name}({tier}티어 출석체크)", false);
                }

                var body = new ValueRange { Values = pieceData };

                var req = SheetManager.Service.Spreadsheets.Values.Update(body, SheetManager.PieceSpreadSheetId, "시트1!B6");
                req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                LogManager.Log("[Check Attendance] Google docs api executed");
                req.Execute();

                AttendanceList = null;
                LogManager.Log("[Check Attendance] Complete");
            }
        }

        // =젠 곡(0)
        private static void ChackSong(OnMessageReceivedArgs arg, string[] cmdarg)
        {
            var request = SongList.FirstOrDefault(x => x.UserName == arg.ChatMessage.DisplayName);

            if (request is null)
            {
                client.SendMessage(arg.ChatMessage.Channel, "아직 곡을 신청하지 않았습니다!");
                return;
            }

            client.SendMessage(arg.ChatMessage.Channel, $"{arg.ChatMessage.DisplayName}님의 신청곡은 현재 {SongList.IndexOf(request) + 1}번째에 있습니다! ({request.SongName})");
        }

        // =젠 아이돌(0)
        private static void GetIdolInfo(OnMessageReceivedArgs arg, string[] cmdarg)
        {
            // 시노미야 카렌 / 16세 / 도쿄 출신 / 159cm, 48kg / AB형 / 90-59-90 / 엔젤 타입 / CV.콘도 유이 / (설명)
            var req = SheetManager.IdolInfoSheet;

            if (req == null)
            {
                client.SendMessage(arg.ChatMessage.Channel, "아이돌 정보 DB가 비어있어요!");
                return;
            }

            var selected = req[new Random().Next(req.Count)];

            var res = new List<string>();
            for (var i = 0; i < selected.Count; i++)
            {
                if (selected[i] == null || selected[i] as string == string.Empty)
                    continue;

                switch (i)
                {
                    case 1:
                        res[0] += ($" ({selected[i] as string} 프로)");
                        break;
                    case 2:
                        res.Add($"{selected[i] as string}세");
                        break;
                    case 3:
                        res.Add($"{selected[i] as string} 출신");
                        break;
                    case 4:
                        if (selected[i] as string == "(불명)")
                        {
                            if (selected[i + 1] as string == "(불명)")
                            {
                                res.Add("신장, 체중 불명");
                            }
                            else
                            {
                                res.Add($"{selected[i] as string}cm / 체중 불명");
                            }
                        }
                        else if (selected[i + 1] as string == "(불명)")
                        {
                            res.Add($"{selected[i] as string}kg / 신장 불명");
                        }
                        else
                        {
                            res.Add($"{selected[i] as string}cm / {selected[i + 1] as string}kg");
                        }
                        break;
                    case 5:
                        continue;
                    case 6:
                        res.Add($"{selected[i] as string}형");
                        break;
                    case 8:
                        res.Add($"{selected[i] as string} 타입");
                        break;
                    case 9:
                        res.Add($"CV. {selected[i] as string}");
                        break;
                    default:
                        res.Add(selected[i] as string);
                        break;
                }
            }

            client.SendMessage(arg.ChatMessage.Channel, string.Join(" / ", res));
        }
    }
}