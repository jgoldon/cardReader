using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using System.IO;
using System.Text;
using PCSC;

namespace CardReader
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public IList<string> Readers { get; set; }
        public IList<string> Logs { get; set; }

        public string CurrentReader
        {
            get { return _currentReader; }
            set
            {
                _currentReader = value;
                OnPropertyChanged();
            }
        }

        public string Status
        {
            get { return _status; }
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        private readonly SCardReader _sCardReader;
        private IntPtr _protocol;
        private readonly SCardContext _sCardContext;
        private string _status;
        private string _currentReader;

        public MainWindow()
        {
            InitializeComponent();
            SMSbutton.Visibility = Visibility.Hidden;
            Dispatcher.UnhandledException += LogError;
            DataContext = this;

            Readers = new ObservableCollection<string>();
            Logs = new ObservableCollection<string>();
            Status = "NotConnected";
   

            _sCardContext = new SCardContext();
            _sCardContext.Establish(SCardScope.System);
            _sCardReader = new SCardReader(_sCardContext);
        }

        private void LogError(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log("Error. Exception: {0} with message: {1}", e.Exception.GetType().Name, e.Exception.Message);
            e.Handled = true;
        }

        private void LoadReaders(object sender, EventArgs e)
        {
            var readers = _sCardContext.GetReaders();

            Log("Card readers loaded.");

            Readers.Clear();

            foreach (var reader in readers)
            {
                Readers.Add(reader);
            }

            if (readers.Length > 0)
            {
                CurrentReader = readers[0];
            }
            if (CurrentReader != null)
                SMSbutton.Visibility = Visibility.Visible;
        }

        private void ConnectCard(object sender, EventArgs e)
        {
            //if(_sCardReader.IsConnected)
              //  _sCardReader.Disconnect(SCardReaderDisposition.Leave);
            if (CurrentReader == null)
                return;
            var result = _sCardReader.Connect(CurrentReader,
                SCardShareMode.Shared,
                SCardProtocol.Any);

            Status = result.ToString();

            Log("Card connect attempt: " + Status);
            if (result == SCardError.Success)
            {
                SetProtocol();
            }
        }

        private void SetProtocol()
        {
            switch (_sCardReader.ActiveProtocol)
            {
                case SCardProtocol.T0:
                    _protocol = SCardPCI.T0;
                    break;
                case SCardProtocol.T1:
                    _protocol = SCardPCI.T1;
                    break;
                default:
                    throw new PCSCException(SCardError.ProtocolMismatch, "Not supported protocol: " + _sCardReader.ActiveProtocol.ToString());
            }

            //SendCommand(new byte[] { 0xA0, 0xA4, 0x00, 0x00, 0x02, 0x7F, 0x10 }); //przykladowa komenda
        }

        private void Log(string log, params object[] param)
        {
            Logs.Insert(0, string.Format(log + " #" + DateTime.Now, param));
        }

        private void SendCommand(byte[] command)
        {
            Log("Sending command: {0}", string.Join(" ", command.Select(x => string.Format("{0:X2} ", x))));

            byte[] response = new byte[256];
            _sCardReader.Transmit(_protocol, command, ref response);

            Log("Command response: {0}", string.Join(" ", response.Select(x => string.Format("{0:X2} ", x))));
        }

        private void ReadSMSCommand(byte[] command)
        {
            Log("Sending command: {0}", string.Join(" ", command.Select(x => string.Format("{0:X2} ", x))));

            byte[] response = new byte[256];
            _sCardReader.Transmit(_protocol, command, ref response);
            byte[] a = {0xD7, 0x27, 0xD3, 0x78, 0x0C, 0x3A, 0x8F, 0xFF};
           // SMSbox.Text = Encoding.GSM7.GetString(a);

            Encoding gsmEnc = new Mediaburst.Text.GSMEncoding();
            Encoding utf8Enc = new System.Text.UTF8Encoding();

           
            
            //SMSbox.Text = response.ToString();
            //var b = Encoding.UTF8.GetChars(a);
            var c = string.Join(" ", response.Select(x => string.Format("{0:X2} ", x)));
            SMSbox.Text = c;
            Log("SMS: {0}", string.Join(" ", response.Select(x => string.Format("{0:X2} ", x))));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if(PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        protected override void OnClosed(EventArgs e)
        {
            _sCardContext.Dispose();
            base.OnClosed(e);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //SendCommand(new byte[] { 0xA0, 0x20, 0x00, 0x01, 0x08, 0x31, 0x34, 0x32, 0x36, 0xFF, 0xFF, 0xFF, 0xFF });//pin
            SendCommand(new byte[] { 0xA0, 0xA4, 0x00, 0x00, 0x02, 0x7F, 0x10 });//select telecom
            SendCommand(new byte[] { 0xA0, 0xC0, 0x00, 0x00, 0x16 });//get response
            SendCommand(new byte[] { 0xA0, 0xA4, 0x00, 0x00, 0x02, 0x6F, 0x3C });//select sms
            SendCommand(new byte[] { 0xA0, 0xC0, 0x00, 0x00, 0x0F });//get response
            ReadSMSCommand(new byte[] { 0xA0, 0xB2, 0x01, 0x04, 0xB0 });//read
        }
    }
}
