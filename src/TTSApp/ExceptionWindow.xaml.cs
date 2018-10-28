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

namespace TTSApp {
    /// <summary>
    /// Interaction logic for ExceptionWindow.xaml
    /// </summary>
    public partial class ExceptionWindow : Window {

        public static void Open(Exception e)
        {
            var window = new ExceptionWindow();
            window.Exception = e;

            window.Show();
        }

        public ExceptionWindow() {
            InitializeComponent();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            ExceptionTextBox.Text = Exception.ToString();
        }

        public Exception Exception { get; set; }
    }
}
