using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using System.Windows.Input;
using System.Windows.Media;

namespace ZennMusic
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    // ReSharper disable once InheritdocConsiderUsage
    // ReSharper disable once UnusedMember.Global
    // ReSharper disable once RedundantExtendsListEntry
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LogManager.ActivateLogger();
            
            AppDomain.CurrentDomain.UnhandledException += (e, arg) =>
            {
                var ex = arg.ExceptionObject as Exception;
                LogManager.Log(ex?.Message);
                LogManager.Log(ex?.StackTrace, false);
            };

            SheetManager.InitializeSheet();
            ChatManager.InitializeCommand();
            ChatManager.InitializeChatManager();

            ChatManager.SongList.CollectionChanged += (sender, e) => 
                SongCountText.Text = $"현재 {ChatManager.SongList.Count}개의 곡이 신청되었습니다.";

            SongRequestListBox.ItemsSource = ChatManager.SongList;

            FontComboBox.SelectedIndex = FontComboBox.Items.Cast<FontFamily>()
                                                           .ToList()
                                                           .FindIndex(x => x.FamilyNames.Select(n => n.Value.ToLower().Replace(" ", "")).Contains("나눔고딕"));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            LogManager.Log("[Next Song] Button clicked");

            if (ChatManager.SongList.Count > 0)
            {
                LogManager.Log(
                    $"[Next Song] Data : {ChatManager.SongList[0].UserName}|{ChatManager.SongList[0].SongName}|{ChatManager.SongList[0].Payment}");
                ChatManager.DeletedSongList.Add(ChatManager.SongList[0]);
                ChatManager.SongList.RemoveAt(0);

                if (ChatManager.DeletedSongList.Count > 50)
                    ChatManager.DeletedSongList.RemoveAt(0);
            }

            LogManager.Log("[Next Song] Complete");
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            LogManager.Log("[Custom Song] Button clicked");

            if (CustomInputBox.Text != string.Empty)
            {
                ChatManager.SongList.Add(new SongRequest(CustomInputBox.Text, "zenn", SongRequestPayment.Special));
                LogManager.Log($"[Custom song] Data : {CustomInputBox.Text}");
                CustomInputBox.Text = string.Empty;
            }

            LogManager.Log("[Custom song] Complete");
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            LogManager.Log("[Change Sheet] Button clicked");
            var dialog = new SheetIdChangeDialog();
            if (dialog.ShowDialog() == true)
            {
                SheetManager.PieceSpreadSheetId = dialog.ResponseText;
                LogManager.Log($"[Change Sheet] Data : {dialog.ResponseText}");
            }
            LogManager.Log("[Change Sheet] Complete");
        }

        private void CustomInputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {

        }

        private void ItemMenu_Delete(object sender, RoutedEventArgs e)
        {
            LogManager.Log("[Delete Song] Button clicked");

            if (SongRequestListBox.SelectedIndex == -1) return;

            if (!(SongRequestListBox.SelectedItem is SongRequest selectedItem))
                return;

            LogManager.Log(
                $"[Delete Song] Data : {selectedItem.UserName}|{selectedItem.SongName}|{selectedItem.Payment}");
            ChatManager.SongList.Remove(selectedItem);

            LogManager.Log("[Delete Song] Complete");
        }

        private void ItemMenu_Refund(object sender, RoutedEventArgs e)
        {
            LogManager.Log("[Refund Song] Button clicked");
            if (SongRequestListBox.SelectedIndex == -1) return;

            if (!(SongRequestListBox.SelectedItem is SongRequest selectedItem))
                return;

            LogManager.Log(
                $"[Refund Song] Data : {selectedItem.UserName}|{selectedItem.SongName}|{selectedItem.Payment}");

            ChatManager.SongList.Remove(selectedItem);

            if (selectedItem.Payment == SongRequestPayment.Special)
                return;

            int type;
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
                default:
                    return;
            }

            var pieceData = SheetManager.PieceSheet;
            var search = pieceData
                .FirstOrDefault(x => (x[0] as string)?.Replace(" ", "") == selectedItem.UserName);

            var range = $"시트1!B{pieceData.ToList().FindIndex(x => x[0] as string == selectedItem.UserName) + 6}";

            if (search == null)
                return;

            search[type] = int.Parse(search[type] as string ?? "0") + (type == 1 ? 3 : 1);

            var body = new ValueRange
            {
                Values = new List<IList<object>>
                {
                    new List<object> {null, search[1], search[2]}
                }
            };

            var req = SheetManager.Service.Spreadsheets.Values.Update(body, SheetManager.PieceSpreadSheetId, range);
            req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            LogManager.Log("[Refund Song] Google docs api request executed");
            req.Execute();

            LogManager.Log("[Refund Song] Complete");
        }

        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            LogManager.Log("[Request Toggle] On");
            ChatManager.IsRequestAvailable = true;
        }

        private void ToggleButton_OnUnchecked(object sender, RoutedEventArgs e)
        {
            LogManager.Log("[Request Toggle] Off");
            ChatManager.IsRequestAvailable = false;
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if ((e.Key == Key.W || e.Key == Key.S) && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (SongRequestListBox.SelectedItem == null || !(SongRequestListBox.SelectedItem is SongRequest request))
                    return;

                var isUp = e.Key == Key.W;

                var index = ChatManager.SongList.IndexOf(request);
                var newIndex = index + (isUp ? -1 : 1);

                if (newIndex < 0) return;
                if (newIndex >= ChatManager.SongList.Count) return;

                ChatManager.SongList.Move(index, newIndex);
            }
            else if(e.Key == Key.Enter)
            {
                Button_Click_1(null, null);
            }
        }

        private void FontComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            Resources["MyFont"] = FontComboBox.SelectedItem as FontFamily;
        }
    }
}
