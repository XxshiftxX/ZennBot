﻿using System;
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
        public static readonly string[] ZennEmotes = {"produc1Keut", "produc1Nano", "produc1Vwai"};
        public static bool IsRequestAvailable = false;

        public static List<(string name, int tier)> AttandanceList = null;

        public static void InitializeChatManager()
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            var filePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "data.txt");
            var fileRaw = File.ReadAllText(filePath);
            var decoded = Convert.FromBase64String(fileRaw);
            var datas = Encoding.UTF8.GetString(decoded).Split('|').Select(x => new string(x.Reverse().ToArray())).ToArray();

            var botId = datas[0];
            var botToken = datas[1];

            api.Settings.ClientId = datas[2];
            api.Settings.AccessToken = datas[3];
            api.Settings.Secret = datas[4];

            var credentials = new ConnectionCredentials(botId, botToken);

            client.Initialize(credentials, "producerzenn");

            client.OnError += (sender, e) => Console.WriteLine($@"[ERROR] {e.Exception}");
            client.OnMessageReceived += OnMessageReceived;

            client.OnGiftedSubscription += (e, arg) => client.SendMessage(arg.Channel, "저격이다! 읖드려!!!");

            client.Connect();
        }

        public static void InitializeCommand()
        {
            Commands["조각"] = GetPiece;
            Commands["지급"] = PayPiece;
            Commands["신청"] = RequestSong;

            Commands["출석"] = CheckAttendance;

            Commands["호두라이브"] = (arg, cmdarg) => client.SendMessage(arg.ChatMessage.Channel, "재밌었다 ㅎㅎ");
            Commands["초코캠프"] = (arg, cmdarg) => client.SendMessage(arg.ChatMessage.Channel, "기대하고 있어요 ㅎㅎㅎ");
            Commands["젠하"] = (arg, cmdarg) => client.SendMessage(arg.ChatMessage.Channel, "삐빅- 젠하트하-");
        }

        private static void OnMessageReceived(object sender, OnMessageReceivedArgs args)
        {
            if (args.ChatMessage.Message.Split()[0] == "=젠")
            {
                var commandArgs = args.ChatMessage.Message.Split().Skip(1).ToArray();

                if (!Commands.ContainsKey(commandArgs[0]))
                    client.SendMessage(args.ChatMessage.Channel, "존재하지 않는 명령어입니다!");
                else
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        Commands[commandArgs[0]](args, commandArgs));
            }
            else if (AttandanceList != null)
            {
                var lev = args.ChatMessage.Badges.First(x => x.Key == "subscriber").Value;
                var tier = 0;
                args.ChatMessage.EmoteSet.Emotes.Exists(x => ZennEmotes.Contains(x.Name));

                if (args.ChatMessage.EmoteSet.Emotes.Exists(x => x.Name == "produc1Keut"))
                    tier = 1;
                else if (args.ChatMessage.EmoteSet.Emotes.Exists(x => x.Name == "produc1Nano"))
                    tier = 2;
                else if (args.ChatMessage.EmoteSet.Emotes.Exists(x => x.Name == "produc1Vwai"))
                    tier = 3;

                if (tier == 0)
                    return;
                AttandanceList.Add((args.ChatMessage.DisplayName, tier));
            }
        }

        private static void ProcessNullName(OnMessageReceivedArgs args, string funcType, bool doLog = true)
        {
            if (doLog)
                client.SendMessage(args.ChatMessage.Channel, "신청곡 조각 시트에 이름이 존재하지 않아요!");

            var nullData = SheetManager.NullNameSheet;

            nullData.Add(funcType);

            var nullBody = new ValueRange
            {
                Values = nullData.Select(x => new List<object> { x } as IList<object>).ToList()
            };

            var nullreq = SheetManager.Service.Spreadsheets.Values.Update(nullBody, SheetManager.SpreadSheetId,
                "시트1!J6");
            nullreq.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            nullreq.Execute();
        }

        // =젠 조각(0)
        private static void GetPiece(OnMessageReceivedArgs args, string[] commandArgs)
        {
            var pieceData = SheetManager.PieceSheet;
            var search = pieceData
                .FirstOrDefault(x => (x[0] as string)?.Replace(" ", "") == args.ChatMessage.DisplayName);

            if (search is null)
            {
                ProcessNullName(args, $"{args.ChatMessage.DisplayName}(조각 조회)");
            }
            else
            {
                client.SendMessage(args.ChatMessage.Channel,
                    $"{args.ChatMessage.DisplayName}님의 신청곡 조각 : " +
                    $"{(search.Count > 1 ? (search[1] ?? 0) : 0)} / 신청곡 티켓 : {(search.Count > 2 ? (search[2] ?? 0) : 0)}" +
                    $"{((search.Count > 3 && (search[3] as string) != string.Empty) ? (" (" + search[3] + ")") : string.Empty)}");
            }
        }

        // =젠 지급(0) 곡(1) 시프트(2) 020(3)
        private static void PayPiece(OnMessageReceivedArgs args, string[] commandArgs)
        {
            var PieceCount = 1;

            if (!ManagerNameList.Contains(args.ChatMessage.Username))
            {
                client.SendMessage(args.ChatMessage.Channel, "권한이 없습니다!");
                return;
            }
            if (commandArgs.Length < 3)
            {
                client.SendMessage(args.ChatMessage.Channel, "잘못된 명령어 형식입니다. \"=젠 지급 (조각/곡) 닉네임 [갯수]\"의 형식으로 입력해주세요.");
                return;
            }
            if (commandArgs.Length >= 4)
            {
                if (!int.TryParse(commandArgs[3], out PieceCount))
                {
                    client.SendMessage(args.ChatMessage.Channel, "갯수 입력 부분에는 숫자를 입력해주세요!");
                }
                else if (PieceCount < 1)
                {
                    client.SendMessage(args.ChatMessage.Channel, "왜 남의 조각을 뺏어가려고 하세요? ㅡㅡ 수량은 1 이상으로 입력하세요!");
                }

                return;
            }

            var pieceData = SheetManager.PieceSheet;
            var search = pieceData
                .FirstOrDefault(x => (x[0] as string)?.Replace(" ", "") == commandArgs[2]);

            if (search is null)
            {
                ProcessNullName(args, $"{args.ChatMessage.DisplayName}(지급-{commandArgs[1]})");
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
                    client.SendMessage(args.ChatMessage.Channel, "잘못된 명령어 형식입니다. \"=젠 지급 (조각/곡) 닉네임 [갯수]\"의 형식으로 입력해주세요.");
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
            req.Execute();

            client.SendMessage(args.ChatMessage.Channel, $"{commandArgs[2]}님께 {(type == 1 ? "조각" : "신청곡")} {PieceCount}개가 지급되었습니다.");
        }

        // =젠 신청(0) 인페르노 스퀘어링(1~)
        private static void RequestSong(OnMessageReceivedArgs args, string[] commandArgs)
        {
            if (!IsRequestAvailable)
            {
                client.SendMessage(args.ChatMessage.Channel, "현재 신청곡을 받고있지 않아요!");
                return;
            }
            if (commandArgs.Length < 2)
            {
                client.SendMessage(args.ChatMessage.Channel, "신청곡의 이름을 입력해주세요!");
                return;
            }

            var pieceData = SheetManager.PieceSheet;
            var search = pieceData
                .FirstOrDefault(x => (x[0] as string)?.Replace(" ", "") == args.ChatMessage.DisplayName);

            var song = string.Join(" ", commandArgs.Skip(1));

            if (search is null)
            {
                ProcessNullName(args, $"{args.ChatMessage.DisplayName}(신청곡)");
                return;
            }

            if (SongList.Reverse().Take(4).Any(x => x.UserName == args.ChatMessage.DisplayName))
            {
                client.SendMessage(args.ChatMessage.Channel, "아직 쿨타임이에요! 이전에 신청한 곡과 현재 신청하는 곡 사이에 최소 4개의 곡이 있어야 해요!");
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
            req.Execute();

            SongList.Add(new SongRequest(song, args.ChatMessage.DisplayName, reqPayment));
        }

        // =젠 출석(0)
        private static void CheckAttendance(OnMessageReceivedArgs arg, string[] cmdarg)
        {
            if (!ManagerNameList.Contains(arg.ChatMessage.Username))
            {
                client.SendMessage(arg.ChatMessage.Channel, "권한이 없습니다!");
                return;
            }

            if (AttandanceList != null)
            {
                client.SendMessage(arg.ChatMessage.Channel, "== 출석 체크가 종료되었습니다! ==");

                var pieceData = SheetManager.PieceSheet;

                foreach (var attandace in AttandanceList)
                {
                    var user = pieceData.FirstOrDefault(x => (x[0] as string) == attandace.name);
                    var count = attandace.tier;

                    if (user != null)
                    {
                        user[1] = int.Parse(user[1] as string ?? "0") + count;
                    }
                    else
                    {
                        ProcessNullName(arg, $"{attandace.name}({attandace.tier + 1}티어 출석체크)", false);
                    }
                }
                
                var body = new ValueRange
                {
                    Values = pieceData
                };

                var req = SheetManager.Service.Spreadsheets.Values.Update(body, SheetManager.SpreadSheetId, "시트1!B6");
                req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                req.Execute();
                
                AttandanceList = null;
            }
            else
            {
                client.SendMessage(arg.ChatMessage.Channel, "== 출석 체크가 시작되었습니다! ==");
                AttandanceList = new List<(string, int)>();
            }
        }

    }
}