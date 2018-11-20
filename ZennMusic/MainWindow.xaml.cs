using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace ZennMusic
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public static object SonglistLocker = new Object();
        public MainWindow()
        {
            InitializeComponent();

            AppDomain.CurrentDomain.UnhandledException += (e, arg) =>
            {
                var path = @"D:\zennLog.txt";
                if (!File.Exists(path))
                    File.Create(path);

                using (var tw = new StreamWriter(path, true))
                {
                    var ex = arg.ExceptionObject as Exception;
                    tw.WriteLine(ex?.Message);
                    tw.WriteLine();
                    tw.WriteLine(ex?.StackTrace);
                }
            };

            SheetManager.InitService();
            ChatManager.InitializeCommand();
            ChatManager.InitializeChatManager();

            SongRequestListBox.ItemsSource = ChatManager.SongList;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if(ChatManager.SongList.Count > 0)
                ChatManager.SongList.RemoveAt(0);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            lock (SonglistLocker)
                if (CustomInputBox.Text != string.Empty)
                {
                    ChatManager.SongList.Add(new SongRequest(CustomInputBox.Text, "zenn", SongRequestPayment.Special));
                    CustomInputBox.Text = string.Empty;
                }
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new SheetIdChangeDialog();
            if (dialog.ShowDialog() == true)
            {
                SheetManager.SpreadSheetId = dialog.ResponseText;
            }
        }

        private void CustomInputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            
        }

        private void ItemMenu_Delete(object sender, RoutedEventArgs e)
        {
            if (SongRequestListBox.SelectedIndex == -1) return;

            var selectedItem = SongRequestListBox.SelectedItem as SongRequest;
            ChatManager.SongList.Remove(selectedItem);
        }

        private void ItemMenu_Refund(object sender, RoutedEventArgs e)
        {
            if (SongRequestListBox.SelectedIndex == -1) return;

            var selectedItem = SongRequestListBox.SelectedItem as SongRequest;
            ChatManager.SongList.Remove(selectedItem);

            if (selectedItem.Payment == SongRequestPayment.Special)
                return;

            var type = 0;
            switch (selectedItem.Payment)
            {
                case SongRequestPayment.Piece:
                    type = 1;
                    break;
                case SongRequestPayment.Ticket:
                    type = 2;
                    break;
                case SongRequestPayment.Special:
                    return;
            }

            var pieceData = SheetManager.PieceSheet;
            var search = pieceData
                .FirstOrDefault(x => (x[0] as string)?.Replace(" ", "") == selectedItem.UserName);

            var range = $"시트1!B{pieceData.ToList().FindIndex(x => x[0] as string == selectedItem.UserName) + 6}";

            search[type] = int.Parse(search[type] as string ?? "0") + (type == 1 ? 3 : 1);

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
        }

        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            ChatManager.IsRequestAvailable = true;
        }

        private void ToggleButton_OnUnchecked(object sender, RoutedEventArgs e)
        {
            ChatManager.IsRequestAvailable = false;
        }
    }
}
