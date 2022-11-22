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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CViewer
{
    /// <summary>
    /// WaitingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class WaitingWindow : Window
    {
        int tick = 0;
        string statusString;
        public WaitingWindow(string _statusString)
        {
            InitializeComponent();
            statusString = _statusString;

            DispatcherTimer dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            dispatcherTimer.Start();
        }

        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            tbkDot.Text = statusString + ((tick == 1) ? "." : "") + ((tick == 2) ? ".." : "") + ((tick == 3) ? "..." : "");
            if (tick == 3)
            {
                tick = 0;
            }
            else
            {
                tick++;
            }
        }
    }
}
