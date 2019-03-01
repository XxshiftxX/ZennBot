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
        private static readonly string[] ManagerNameList = { "producerzenn", "qjfrntop", "freewing8101", "flashman0509", "mohamgwa1" };
        private static readonly Dictionary<string, Action<OnMessageReceivedArgs, string[]>> Commands =
            new Dictionary<string, Action<OnMessageReceivedArgs, string[]>>();
        public static readonly ObservableCollection<SongRequest> SongList = new ObservableCollection<SongRequest>();
        public static readonly List<SongRequest> DeletedSongList = new List<SongRequest>();
        public static bool IsRequestAvailable = false;

        public static List<(string name, int tier)> AttendanceList;

        public static void InitializeChatManager()
        {
            var currentPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) ?? "D:\\";
            var filePath = Path.Combine(currentPath, "data.txt");
            var fileRaw = File.ReadAllText(filePath);
            var decoded = Convert.FromBase64String(fileRaw);
            var data = Encoding.UTF8.GetString(decoded).Split('|').Select(x => new string(x.Reverse().ToArray())).ToArray();

            var botId = data[0];
            var botToken = data[1];

            api.Settings.ClientId = data[2];
            api.Settings.AccessToken = data[3];
            api.Settings.Secret = data[4];

            var credentials = new ConnectionCredentials(botId, botToken);

            client.Initialize(credentials, "qjfrntop");

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
            Commands["생일"] = IdolBirthday;

            LogManager.Log("[Command System Initialize] Complete");
        }

        public static void OnMessageReceived(object sender, OnMessageReceivedArgs args)
        {
            LogManager.Log($"[Chat Event] ({args.ChatMessage.DisplayName}) {args.ChatMessage.Message}");
            if (args.ChatMessage.Message.Split()[0] == "=젠")
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

            var nullRequest = SheetManager.Service.Spreadsheets.Values.Update(nullBody, SheetManager.SpreadSheetId,
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

            var req = SheetManager.Service.Spreadsheets.Values.Update(body, SheetManager.SpreadSheetId, range);
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

            var req = SheetManager.Service.Spreadsheets.Values.Update(body, SheetManager.SpreadSheetId, range);
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

                var req = SheetManager.Service.Spreadsheets.Values.Update(body, SheetManager.SpreadSheetId, "시트1!B6");
                req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                LogManager.Log("[Check Attendance] Google docs api executed");
                req.Execute();

                AttendanceList = null;
                LogManager.Log("[Check Attendance] Complete");
            }
        }

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

        private static void IdolBirthday(OnMessageReceivedArgs arg, string[] cmdarg)
        {
            String currDate = DateTime.Now.ToString("MMMM dd");
            String[] dateParse = currDate.Split(' ');

            switch (dateParse[0])
            {
                case "January":
                    switch (dateParse[1])
                    {
                        case "1":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 됴묘지 카린, 타카후지 카코의 생일입니다!");
                            break;
                        case "2":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 후유미 쥰의 생일입니다!");
                            break;
                        case "3":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 노노무라 소라와 무라카미 토모에의 생일입니다!");
                            break;
                        case "6":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 미무라 카나코와 안자이 미야코의 생일입니다!");
                            break;
                        case "8":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 에밀리 스튜어트의 생일입니다!");
                            break;
                        case "10":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 세나 시오리의 생일입니다!");
                            break;
                        case "13":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 하마구치 아야메와 하자마 미치오의 생일입니다!");
                            break;
                        case "14":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 권하서의 생일입니다!");
                            break;
                        case "16":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 미츠미네 유이카의 생일입니다!");
                            break;
                        case "18":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 키타자와 시호의 생일입니다!");
                            break;
                        case "19":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 메어리 코크란의 생일입니다!");
                            break;
                        case "21":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 시죠 타카네와 후쿠야마 마이와 마츠야마 쿠미코의 생일입니다!");
                            break;
                        case "23":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사이온지 코토카와 이예은의 생일입니다!");
                            break;
                        case "27":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 마카베 미즈키와 카미야 유키히로의 생일입니다!");
                            break;
                        case "28":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 오오타 유유의 생일입니다!");
                            break;
                        default:
                            client.SendMessage(arg.ChatMessage.Channel, "오늘 생일인 아이돌이 없습니다...");
                            break;
                    }
                case "February":
                    switch (dateParse[1])
                    {
                        case "2":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 타카죠 쿄지의 생일입니다!");
                            break;
                        case "3":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 니노미야 아스카의 생일입니다!");
                            break;
                        case "4":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 토쿠가와 마츠리와 시라유키 치요의 생일입니다!");
                            break;
                        case "6":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 에가미 츠바키의 생일입니다!");
                            break;
                        case "7":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 토고 아이의 생일입니다!");
                            break;
                        case "8":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 이치하라 니나의 생일입니다!");
                            break;
                        case "10":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 히메노 카논의 생일입니다!");
                            break;
                        case "11":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 아사노 후카의 생일입니다!");
                            break;
                        case "12":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 요코야마 나오의 생일입니다!");
                            break;
                        case "14":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 미야모토 프레데리카와 아이하라 유키노와 이쥬인 호쿠토의 생일입니다!");
                            break;
                        case "16":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 테라모토 유키카의 생일입니다!");
                            break;
                        case "17":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 키타가와 마히로의 생일입니다!");
                            break;
                        case "19":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 유사 코즈에의 생일입니다!");
                            break;
                        case "20":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 하코자키 세리카의 생일입니다!");
                            break;
                        case "22":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 마에카와 미쿠의 생일입니다!");
                            break;
                        case "23":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 텐도 테루의 생일입니다!");
                            break;
                        case "24":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 키류 츠카사와 소노다 치요코의 생일입니다!");
                            break;
                        case "25":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 키사라기 치하야와 미후네 미유와 츠키오카 코가네의 생일입니다!");
                            break;
                        case "26":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 쿠로사와 치아키의 생일입니다!");
                            break;
                        default:
                            client.SendMessage(arg.ChatMessage.Channel, "오늘 생일인 아이돌이 없습니다...");
                            break;
                    }
                case "March":
                    switch (dateParse[1])
                    {
                        case "1":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 한다 로코의 생일입니다!");
                            break;
                        case "3":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 이마이 카나와 아마가세 토우마의 생일입니다!");
                            break;
                        case "4":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 카자노 히오리의 생일입니다!");
                            break;
                        case "5":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 코세키 레이나의 생일입니다!");
                            break;
                        case "6":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 아리우라 칸나의 생일입니다!");
                            break;
                        case "7":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 미즈타니 에리와 카타기리 사나에와 아쿠노 히데오의 생일입니다!");
                            break;
                        case "9":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 쿠도 시노부의 생일입니다!");
                            break;
                        case "12":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 주니의 생일입니다!");
                            break;
                        case "13":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 호리 유코의 생일입니다!");
                            break;
                        case "16":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 야나세 미유키의 생일입니다!");
                            break;
                        case "18":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나나오 유리코와 에토 미사키의 생일입니다!");
                            break;
                        case "19":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나가토미 하스미의 생일입니다!");
                            break;
                        case "20":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 오오니시 유리코와 이수지의 생일입니다!");
                            break;
                        case "21":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 마츠오 치즈루와 허영주의 생일입니다!");
                            break;
                        case "22":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사타케 미나코와 와타나베 미노리의 생일입니다!");
                            break;
                        case "23":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나카노 유카와 이지원의 생일입니다!");
                            break;
                        case "25":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 타카츠키 야요이와 타카미네 노아와 오카무라 나오의 생일입니다!");
                            break;
                        case "27":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사쿠라모리 카오리와 무라마츠 사쿠라의 생일입니다!");
                            break;
                        case "28":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 시라사카 코우메의 생일입니다!");
                            break;
                        case "30":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 후쿠다 노리코와 오오누마 쿠루미와 와카자토 하루나의 생일입니다!");
                            break;
                        default:
                            client.SendMessage(arg.ChatMessage.Channel, "오늘 생일인 아이돌이 없습니다...");
                            break;
                    }
                //4월 까지 작업: 2019-03-01
                case "April":
                    switch (dateParse[1])
                    {
                        case "1":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 한다 로코의 생일입니다!");
                            break;
                        case "3":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 이마이 카나와 아마가세 토우마의 생일입니다!");
                            break;
                        case "4":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 카자노 히오리의 생일입니다!");
                            break;
                        case "5":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 코세키 레이나의 생일입니다!");
                            break;
                        case "6":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 아리우라 칸나의 생일입니다!");
                            break;
                        case "7":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 미즈타니 에리와 카타기리 사나에와 아쿠노 히데오의 생일입니다!");
                            break;
                        case "9":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 쿠도 시노부의 생일입니다!");
                            break;
                        case "12":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 주니의 생일입니다!");
                            break;
                        case "13":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 호리 유코의 생일입니다!");
                            break;
                        case "16":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 야나세 미유키의 생일입니다!");
                            break;
                        case "18":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나나오 유리코와 에토 미사키의 생일입니다!");
                            break;
                        case "19":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나가토미 하스미의 생일입니다!");
                            break;
                        case "20":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 오오니시 유리코와 이수지의 생일입니다!");
                            break;
                        case "21":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 마츠오 치즈루와 허영주의 생일입니다!");
                            break;
                        case "22":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사타케 미나코와 와타나베 미노리의 생일입니다!");
                            break;
                        case "23":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나카노 유카와 이지원의 생일입니다!");
                            break;
                        case "25":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 타카츠키 야요이와 타카미네 노아와 오카무라 나오의 생일입니다!");
                            break;
                        case "27":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사쿠라모리 카오리와 무라마츠 사쿠라의 생일입니다!");
                            break;
                        case "28":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 시라사카 코우메의 생일입니다!");
                            break;
                        case "30":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 후쿠다 노리코와 오오누마 쿠루미와 와카자토 하루나의 생일입니다!");
                            break;
                        default:
                            client.SendMessage(arg.ChatMessage.Channel, "오늘 생일인 아이돌이 없습니다...");
                            break;
                    }
                case "May":
                    switch (dateParse[1])
                    {
                        case "1":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 한다 로코의 생일입니다!");
                            break;
                        case "3":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 이마이 카나와 아마가세 토우마의 생일입니다!");
                            break;
                        case "4":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 카자노 히오리의 생일입니다!");
                            break;
                        case "5":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 코세키 레이나의 생일입니다!");
                            break;
                        case "6":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 아리우라 칸나의 생일입니다!");
                            break;
                        case "7":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 미즈타니 에리와 카타기리 사나에와 아쿠노 히데오의 생일입니다!");
                            break;
                        case "9":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 쿠도 시노부의 생일입니다!");
                            break;
                        case "12":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 주니의 생일입니다!");
                            break;
                        case "13":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 호리 유코의 생일입니다!");
                            break;
                        case "16":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 야나세 미유키의 생일입니다!");
                            break;
                        case "18":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나나오 유리코와 에토 미사키의 생일입니다!");
                            break;
                        case "19":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나가토미 하스미의 생일입니다!");
                            break;
                        case "20":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 오오니시 유리코와 이수지의 생일입니다!");
                            break;
                        case "21":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 마츠오 치즈루와 허영주의 생일입니다!");
                            break;
                        case "22":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사타케 미나코와 와타나베 미노리의 생일입니다!");
                            break;
                        case "23":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나카노 유카와 이지원의 생일입니다!");
                            break;
                        case "25":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 타카츠키 야요이와 타카미네 노아와 오카무라 나오의 생일입니다!");
                            break;
                        case "27":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사쿠라모리 카오리와 무라마츠 사쿠라의 생일입니다!");
                            break;
                        case "28":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 시라사카 코우메의 생일입니다!");
                            break;
                        case "30":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 후쿠다 노리코와 오오누마 쿠루미와 와카자토 하루나의 생일입니다!");
                            break;
                        default:
                            client.SendMessage(arg.ChatMessage.Channel, "오늘 생일인 아이돌이 없습니다...");
                            break;
                    }
                case "June":
                    switch (dateParse[1])
                    {
                        case "1":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 한다 로코의 생일입니다!");
                            break;
                        case "3":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 이마이 카나와 아마가세 토우마의 생일입니다!");
                            break;
                        case "4":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 카자노 히오리의 생일입니다!");
                            break;
                        case "5":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 코세키 레이나의 생일입니다!");
                            break;
                        case "6":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 아리우라 칸나의 생일입니다!");
                            break;
                        case "7":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 미즈타니 에리와 카타기리 사나에와 아쿠노 히데오의 생일입니다!");
                            break;
                        case "9":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 쿠도 시노부의 생일입니다!");
                            break;
                        case "12":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 주니의 생일입니다!");
                            break;
                        case "13":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 호리 유코의 생일입니다!");
                            break;
                        case "16":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 야나세 미유키의 생일입니다!");
                            break;
                        case "18":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나나오 유리코와 에토 미사키의 생일입니다!");
                            break;
                        case "19":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나가토미 하스미의 생일입니다!");
                            break;
                        case "20":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 오오니시 유리코와 이수지의 생일입니다!");
                            break;
                        case "21":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 마츠오 치즈루와 허영주의 생일입니다!");
                            break;
                        case "22":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사타케 미나코와 와타나베 미노리의 생일입니다!");
                            break;
                        case "23":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나카노 유카와 이지원의 생일입니다!");
                            break;
                        case "25":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 타카츠키 야요이와 타카미네 노아와 오카무라 나오의 생일입니다!");
                            break;
                        case "27":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사쿠라모리 카오리와 무라마츠 사쿠라의 생일입니다!");
                            break;
                        case "28":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 시라사카 코우메의 생일입니다!");
                            break;
                        case "30":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 후쿠다 노리코와 오오누마 쿠루미와 와카자토 하루나의 생일입니다!");
                            break;
                        default:
                            client.SendMessage(arg.ChatMessage.Channel, "오늘 생일인 아이돌이 없습니다...");
                            break;
                    }
                case "July":
                    switch (dateParse[1])
                    {
                        case "1":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 한다 로코의 생일입니다!");
                            break;
                        case "3":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 이마이 카나와 아마가세 토우마의 생일입니다!");
                            break;
                        case "4":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 카자노 히오리의 생일입니다!");
                            break;
                        case "5":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 코세키 레이나의 생일입니다!");
                            break;
                        case "6":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 아리우라 칸나의 생일입니다!");
                            break;
                        case "7":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 미즈타니 에리와 카타기리 사나에와 아쿠노 히데오의 생일입니다!");
                            break;
                        case "9":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 쿠도 시노부의 생일입니다!");
                            break;
                        case "12":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 주니의 생일입니다!");
                            break;
                        case "13":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 호리 유코의 생일입니다!");
                            break;
                        case "16":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 야나세 미유키의 생일입니다!");
                            break;
                        case "18":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나나오 유리코와 에토 미사키의 생일입니다!");
                            break;
                        case "19":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나가토미 하스미의 생일입니다!");
                            break;
                        case "20":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 오오니시 유리코와 이수지의 생일입니다!");
                            break;
                        case "21":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 마츠오 치즈루와 허영주의 생일입니다!");
                            break;
                        case "22":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사타케 미나코와 와타나베 미노리의 생일입니다!");
                            break;
                        case "23":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나카노 유카와 이지원의 생일입니다!");
                            break;
                        case "25":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 타카츠키 야요이와 타카미네 노아와 오카무라 나오의 생일입니다!");
                            break;
                        case "27":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사쿠라모리 카오리와 무라마츠 사쿠라의 생일입니다!");
                            break;
                        case "28":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 시라사카 코우메의 생일입니다!");
                            break;
                        case "30":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 후쿠다 노리코와 오오누마 쿠루미와 와카자토 하루나의 생일입니다!");
                            break;
                        default:
                            client.SendMessage(arg.ChatMessage.Channel, "오늘 생일인 아이돌이 없습니다...");
                            break;
                    }
                case "August":
                    switch (dateParse[1])
                    {
                        case "1":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 한다 로코의 생일입니다!");
                            break;
                        case "3":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 이마이 카나와 아마가세 토우마의 생일입니다!");
                            break;
                        case "4":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 카자노 히오리의 생일입니다!");
                            break;
                        case "5":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 코세키 레이나의 생일입니다!");
                            break;
                        case "6":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 아리우라 칸나의 생일입니다!");
                            break;
                        case "7":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 미즈타니 에리와 카타기리 사나에와 아쿠노 히데오의 생일입니다!");
                            break;
                        case "9":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 쿠도 시노부의 생일입니다!");
                            break;
                        case "12":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 주니의 생일입니다!");
                            break;
                        case "13":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 호리 유코의 생일입니다!");
                            break;
                        case "16":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 야나세 미유키의 생일입니다!");
                            break;
                        case "18":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나나오 유리코와 에토 미사키의 생일입니다!");
                            break;
                        case "19":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나가토미 하스미의 생일입니다!");
                            break;
                        case "20":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 오오니시 유리코와 이수지의 생일입니다!");
                            break;
                        case "21":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 마츠오 치즈루와 허영주의 생일입니다!");
                            break;
                        case "22":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사타케 미나코와 와타나베 미노리의 생일입니다!");
                            break;
                        case "23":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나카노 유카와 이지원의 생일입니다!");
                            break;
                        case "25":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 타카츠키 야요이와 타카미네 노아와 오카무라 나오의 생일입니다!");
                            break;
                        case "27":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사쿠라모리 카오리와 무라마츠 사쿠라의 생일입니다!");
                            break;
                        case "28":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 시라사카 코우메의 생일입니다!");
                            break;
                        case "30":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 후쿠다 노리코와 오오누마 쿠루미와 와카자토 하루나의 생일입니다!");
                            break;
                        default:
                            client.SendMessage(arg.ChatMessage.Channel, "오늘 생일인 아이돌이 없습니다...");
                            break;
                    }
                case "September":
                    switch (dateParse[1])
                    {
                        case "1":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 한다 로코의 생일입니다!");
                            break;
                        case "3":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 이마이 카나와 아마가세 토우마의 생일입니다!");
                            break;
                        case "4":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 카자노 히오리의 생일입니다!");
                            break;
                        case "5":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 코세키 레이나의 생일입니다!");
                            break;
                        case "6":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 아리우라 칸나의 생일입니다!");
                            break;
                        case "7":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 미즈타니 에리와 카타기리 사나에와 아쿠노 히데오의 생일입니다!");
                            break;
                        case "9":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 쿠도 시노부의 생일입니다!");
                            break;
                        case "12":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 주니의 생일입니다!");
                            break;
                        case "13":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 호리 유코의 생일입니다!");
                            break;
                        case "16":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 야나세 미유키의 생일입니다!");
                            break;
                        case "18":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나나오 유리코와 에토 미사키의 생일입니다!");
                            break;
                        case "19":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나가토미 하스미의 생일입니다!");
                            break;
                        case "20":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 오오니시 유리코와 이수지의 생일입니다!");
                            break;
                        case "21":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 마츠오 치즈루와 허영주의 생일입니다!");
                            break;
                        case "22":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사타케 미나코와 와타나베 미노리의 생일입니다!");
                            break;
                        case "23":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나카노 유카와 이지원의 생일입니다!");
                            break;
                        case "25":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 타카츠키 야요이와 타카미네 노아와 오카무라 나오의 생일입니다!");
                            break;
                        case "27":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사쿠라모리 카오리와 무라마츠 사쿠라의 생일입니다!");
                            break;
                        case "28":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 시라사카 코우메의 생일입니다!");
                            break;
                        case "30":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 후쿠다 노리코와 오오누마 쿠루미와 와카자토 하루나의 생일입니다!");
                            break;
                        default:
                            client.SendMessage(arg.ChatMessage.Channel, "오늘 생일인 아이돌이 없습니다...");
                            break;
                    }
                case "October":
                    switch (dateParse[1])
                    {
                        case "1":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 한다 로코의 생일입니다!");
                            break;
                        case "3":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 이마이 카나와 아마가세 토우마의 생일입니다!");
                            break;
                        case "4":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 카자노 히오리의 생일입니다!");
                            break;
                        case "5":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 코세키 레이나의 생일입니다!");
                            break;
                        case "6":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 아리우라 칸나의 생일입니다!");
                            break;
                        case "7":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 미즈타니 에리와 카타기리 사나에와 아쿠노 히데오의 생일입니다!");
                            break;
                        case "9":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 쿠도 시노부의 생일입니다!");
                            break;
                        case "12":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 주니의 생일입니다!");
                            break;
                        case "13":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 호리 유코의 생일입니다!");
                            break;
                        case "16":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 야나세 미유키의 생일입니다!");
                            break;
                        case "18":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나나오 유리코와 에토 미사키의 생일입니다!");
                            break;
                        case "19":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나가토미 하스미의 생일입니다!");
                            break;
                        case "20":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 오오니시 유리코와 이수지의 생일입니다!");
                            break;
                        case "21":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 마츠오 치즈루와 허영주의 생일입니다!");
                            break;
                        case "22":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사타케 미나코와 와타나베 미노리의 생일입니다!");
                            break;
                        case "23":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나카노 유카와 이지원의 생일입니다!");
                            break;
                        case "25":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 타카츠키 야요이와 타카미네 노아와 오카무라 나오의 생일입니다!");
                            break;
                        case "27":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사쿠라모리 카오리와 무라마츠 사쿠라의 생일입니다!");
                            break;
                        case "28":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 시라사카 코우메의 생일입니다!");
                            break;
                        case "30":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 후쿠다 노리코와 오오누마 쿠루미와 와카자토 하루나의 생일입니다!");
                            break;
                        default:
                            client.SendMessage(arg.ChatMessage.Channel, "오늘 생일인 아이돌이 없습니다...");
                            break;
                    }
                case "November":
                    switch (dateParse[1])
                    {
                        case "1":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 한다 로코의 생일입니다!");
                            break;
                        case "3":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 이마이 카나와 아마가세 토우마의 생일입니다!");
                            break;
                        case "4":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 카자노 히오리의 생일입니다!");
                            break;
                        case "5":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 코세키 레이나의 생일입니다!");
                            break;
                        case "6":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 아리우라 칸나의 생일입니다!");
                            break;
                        case "7":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 미즈타니 에리와 카타기리 사나에와 아쿠노 히데오의 생일입니다!");
                            break;
                        case "9":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 쿠도 시노부의 생일입니다!");
                            break;
                        case "12":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 주니의 생일입니다!");
                            break;
                        case "13":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 호리 유코의 생일입니다!");
                            break;
                        case "16":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 야나세 미유키의 생일입니다!");
                            break;
                        case "18":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나나오 유리코와 에토 미사키의 생일입니다!");
                            break;
                        case "19":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나가토미 하스미의 생일입니다!");
                            break;
                        case "20":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 오오니시 유리코와 이수지의 생일입니다!");
                            break;
                        case "21":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 마츠오 치즈루와 허영주의 생일입니다!");
                            break;
                        case "22":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사타케 미나코와 와타나베 미노리의 생일입니다!");
                            break;
                        case "23":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나카노 유카와 이지원의 생일입니다!");
                            break;
                        case "25":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 타카츠키 야요이와 타카미네 노아와 오카무라 나오의 생일입니다!");
                            break;
                        case "27":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사쿠라모리 카오리와 무라마츠 사쿠라의 생일입니다!");
                            break;
                        case "28":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 시라사카 코우메의 생일입니다!");
                            break;
                        case "30":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 후쿠다 노리코와 오오누마 쿠루미와 와카자토 하루나의 생일입니다!");
                            break;
                        default:
                            client.SendMessage(arg.ChatMessage.Channel, "오늘 생일인 아이돌이 없습니다...");
                            break;
                    }
                case "December":
                    switch (dateParse[1])
                    {
                        case "1":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 한다 로코의 생일입니다!");
                            break;
                        case "3":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 이마이 카나와 아마가세 토우마의 생일입니다!");
                            break;
                        case "4":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 카자노 히오리의 생일입니다!");
                            break;
                        case "5":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 코세키 레이나의 생일입니다!");
                            break;
                        case "6":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 아리우라 칸나의 생일입니다!");
                            break;
                        case "7":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 미즈타니 에리와 카타기리 사나에와 아쿠노 히데오의 생일입니다!");
                            break;
                        case "9":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 쿠도 시노부의 생일입니다!");
                            break;
                        case "12":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 주니의 생일입니다!");
                            break;
                        case "13":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 호리 유코의 생일입니다!");
                            break;
                        case "16":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 야나세 미유키의 생일입니다!");
                            break;
                        case "18":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나나오 유리코와 에토 미사키의 생일입니다!");
                            break;
                        case "19":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나가토미 하스미의 생일입니다!");
                            break;
                        case "20":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 오오니시 유리코와 이수지의 생일입니다!");
                            break;
                        case "21":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 마츠오 치즈루와 허영주의 생일입니다!");
                            break;
                        case "22":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사타케 미나코와 와타나베 미노리의 생일입니다!");
                            break;
                        case "23":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 나카노 유카와 이지원의 생일입니다!");
                            break;
                        case "25":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 타카츠키 야요이와 타카미네 노아와 오카무라 나오의 생일입니다!");
                            break;
                        case "27":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 사쿠라모리 카오리와 무라마츠 사쿠라의 생일입니다!");
                            break;
                        case "28":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 시라사카 코우메의 생일입니다!");
                            break;
                        case "30":
                            client.SendMessage(arg.ChatMessage.Channel, "오늘은 후쿠다 노리코와 오오누마 쿠루미와 와카자토 하루나의 생일입니다!");
                            break;
                        default:
                            client.SendMessage(arg.ChatMessage.Channel, "오늘 생일인 아이돌이 없습니다...");
                            break;
                    }
                default:
                    break;
            }
            return;

        }
    }
}