using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

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

            var sheet = SheetManager.Sheet;
            MyConsole.Text = string.Join("\n", sheet.Select(x => string.Join("\t", x)));
            ChatManager.InitializeChatManager();

            const string spreadSheetId = "1fndP3ddyqehCIn6vcpEiZOOixzYN6MX8puCnLdOIqgM";
            const string range = "시트1!B6";

            var body = new ValueRange { Values = new List<IList<object>> {new List<object> {null, 10, 10}} };

            var req = SheetManager.Service.Spreadsheets.Values.Update(body, spreadSheetId, range);
            req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

            req.Execute();
        }
    }
}
