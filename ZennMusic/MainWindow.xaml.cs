using System;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Threading;

namespace ZennMusic
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            SheetManager.InitService();
            ChatManager.InitializeCommand();
            ChatManager.InitializeChatManager();

            SongRequestListBox.ItemsSource = ChatManager.SongList;

            var ConsoleRefresh = new DispatcherTimer() { Interval = new TimeSpan(0, 0, 0, 0, 50) };
            ConsoleRefresh.Tick += (e, arg) =>
            {
                if (ChatManager.IsEditingSongList) return;

                ChatManager.IsRefreshingSongList = true;
                SongRequestListBox.Items.Refresh();
                ChatManager.IsRefreshingSongList = false;
            };

            var b = new BlockingCollection<string>();

            ConsoleRefresh.Start();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (ChatManager.SongList.Count <= 0 || ChatManager.IsRefreshingSongList) return;

            ChatManager.IsEditingSongList = true;
            ChatManager.SongList.RemoveAt(0);
            ChatManager.IsEditingSongList = false;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e) => 
            ChatManager.SongList.Add(new SongRequest(CustomInputBox.Text, "zenn", SongRequestPayment.Special));

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new SheetIdChangeDialog();
            if (dialog.ShowDialog() == true)
            {
                SheetManager.SpreadSheetId = dialog.ResponseText;
            }
        }
    }
}
