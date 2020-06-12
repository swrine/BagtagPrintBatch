using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.ComponentModel;
using System.Windows;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Serialization;

using BagTagPrinting;
using BagTagPrinting.Printer;


namespace WpfBtprint2
{
    public struct BagtagPrintData
    {
        public string lpn, flight, dest_airport, dp, passenger, julian_date, date;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string DEFAULT_CONFIG_FILENAME = "IntTest";

        int PRINT_PAUSE_INTERVAL = 2500; //in millisecond

        private System.ComponentModel.BackgroundWorker printWorker;

        string m_strPectab;
        string m_strContentFormat;
        BagtagPrintData[] printDataList;

        private int printDataSize;
        //private int printProgressIndex;

        ManualResetEvent _busy;

        public MainWindow()
        {
            InitializeComponent();

            InitializeBackgroundWorker();

            m_strPectab = "";
            m_strContentFormat = "";
        }

        private void InitializeBackgroundWorker()
        {
            this.printWorker = new System.ComponentModel.BackgroundWorker();

            printWorker.DoWork += new DoWorkEventHandler(printWorker_DoWork);
            printWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(printWorker_RunWorkerCompleted);
            printWorker.ProgressChanged += new ProgressChangedEventHandler(printWorker_ProgressChanged);
            
            //    printWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backgroundWorker1_RunWorkerCompleted);
            printWorker.WorkerSupportsCancellation = true;
            printWorker.WorkerReportsProgress = true;

            _busy = new ManualResetEvent(false);
        }


        private void MainWindow1_Loaded(object sender, RoutedEventArgs e)
        {
            AirportPrinterConfiguration printerConfig = new AirportPrinterConfiguration();
            // Select first one
            AirportPrinterFacade.Instance.Init(AirportPrinterFacade.Instance.PrinterCollection[0].type, printerConfig);

            //TaskScheduler uiThread = TaskScheduler.FromCurrentSynchronizationContext();
            //System.Action LoggingThread = new System.Action(() =>
            //{
            AirportPrinterFacade.Instance.OnPrinterStatus += (PrinterStatus status) =>
            {
                switch (status)
                {
                    case PrinterStatus.ONLINE:
                        logBox.AppendText("Printer is online" + Environment.NewLine);
                        break;
                    case PrinterStatus.OFFLINE:
                        logBox.AppendText("Printer is offline" + Environment.NewLine);
                        break;
                    default:
                        logBox.AppendText("Unknown Printer status" + Environment.NewLine);
                        break;
                }
            };

            AirportPrinterFacade.Instance.OnLog += (string logline) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    logBox.AppendText(logline + Environment.NewLine);
                });
                //AppendLogLine(logline);
                //logBox.AppendText(logline + Environment.NewLine);
            };
            //});
            //Task.Factory.StartNew((t) => LoggingThread(), uiThread);
        }

        private void AppendLogLine(string logline)
        {
            logBox.AppendText(logline + Environment.NewLine);
        }

        private void ReadConfigData(string Filename)
        {
            XmlDocument configDoc = new XmlDocument();
            configDoc.Load(@".\ConfigData\" + Filename + ".xml");

            XmlNode nodes = configDoc.SelectSingleNode("BagtagPrint/Pectab");
            m_strPectab = "";
            foreach (XmlNode node in nodes)
            {
                m_strPectab += node.InnerText;
            }

            nodes = configDoc.SelectSingleNode("BagtagPrint/ContentFormat");
            m_strContentFormat = "";
            foreach (XmlNode node in nodes)
            {
                m_strContentFormat += node.InnerText;
            }            
        }

        private void ReadPrintData(string Filename)
        {
            printDataList = new BagtagPrintData[20000];

            using (var reader = new StreamReader(Filename))
            {
                int i = 0;
                printDataSize = 0;
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(',');
                    printDataList[i].lpn = values[0];
                    printDataList[i].flight = values[1];
                    printDataList[i].dest_airport = values[2];
                    printDataList[i].dp = values[3];
                    printDataList[i].passenger = values[4];
                    printDataList[i].julian_date = values[5];
                    printDataList[i].date = values[6];
                    i++;
                }
                printDataSize = i;
            }
        }

        private void OpenDataFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".csv";
            dlg.Filter = "CSV files (*.csv)|*.csv";
            dlg.InitialDirectory = System.AppDomain.CurrentDomain.BaseDirectory;

            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                logBox.AppendText("BagTag Data read from " + dlg.FileName);
                ReadPrintData(dlg.FileName);
            }            
        }

            private void ConnectCOMPort_Click(object sender, RoutedEventArgs e)
        {
            if (!AirportPrinterFacade.Instance.Connect())
            {
                this.logBox.AppendText("Cannot connect..." + Environment.NewLine);
            }
        }

        private void button_print_Click(object sender, RoutedEventArgs e)
        {
            if ((String)button_print.Content == "Print") //Start Printing
            {
                /* if (m_strContentFormat == "")
                {
                    ReadConfigData(DEFAULT_CONFIG_FILENAME);
                    AirportPrinterFacade.Instance.Output.PrintRawText(m_strPectab);
                } */
                //ReadPrintData();

                _busy.Set();
                button_print.Content = "Pause";
                button_cancel_print.IsEnabled = true;

                printWorker.RunWorkerAsync();
            }
            else if ((String)button_print.Content == "Pause") //To Pause
            {
                _busy.Reset();
                button_print.Content = "Resume";
            }
            else  //To Resume
            {
                _busy.Set();
                button_print.Content = "Pause";
            }
            
        }

        private void button_cancel_print_Click(object sender, RoutedEventArgs e)
        {
            if (printWorker.WorkerSupportsCancellation == true)
            {
                printWorker.CancelAsync();
            }
        }

        private void Pectab_IATARes740Recomm1_Click(object sender, RoutedEventArgs e)
        {
            ReadConfigData("IATARes740Recomm1");
            
            logBox.AppendText("Format:"+m_strContentFormat + "ZZ" + Environment.NewLine);

            AirportPrinterFacade.Instance.Output.PrintRawText(m_strPectab);
        }

        private void Pectab_IntTest_Click(object sender, RoutedEventArgs e)
        {
            ReadConfigData("IntTest");

            logBox.AppendText("Format:" + m_strContentFormat + "ZZ" + Environment.NewLine);

            AirportPrinterFacade.Instance.Output.PrintRawText(m_strPectab);
        }

        private void printWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            BagtagPrintData dataLine;
            string str;

            for (int i = 0; i < printDataSize; i++)
            {
                _busy.WaitOne();
                if (worker.CancellationPending == true)
                {
                    e.Cancel = true;
                    break;
                }
                else
                {
                    // Perform a time consuming operation and report progress.
                    dataLine = printDataList[i];

                    str = m_strContentFormat;
                    str = str.Replace("{LPN}", dataLine.lpn);
                    str = str.Replace("{FLIGHT}", dataLine.flight);
                    str = str.Replace("{DP}", dataLine.dp);
                    str = str.Replace("{DEST_AIRPORT}", dataLine.dest_airport);
                    str = str.Replace("{DATE}", dataLine.date);
                    str = str.Replace("{PASSENGER}", dataLine.passenger);
                    str = str.Replace("{JDATE}", dataLine.julian_date);

                    AirportPrinterFacade.Instance.Output.PrintRawText(str);

                    worker.ReportProgress(1, dataLine.lpn + " sent: " + str);

                    System.Threading.Thread.Sleep(PRINT_PAUSE_INTERVAL);
                }
            }
            
        }

        private void printWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

            this.logBox.AppendText(e.UserState.ToString() + Environment.NewLine);

            //printProgressIndex++;
        }

        private void printWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled == true)
            {
                this.logBox.AppendText("Printing cancelled." + Environment.NewLine);
            }
            else if (e.Error != null)
            {
                this.logBox.AppendText("Error: " + e.Error.Message + Environment.NewLine);
            }
            else
            {
                this.logBox.AppendText("Printing finished." + Environment.NewLine);
            }
            button_print.Content = "Print";
            button_cancel_print.IsEnabled = false;
        }

        private void logBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            logBox.ScrollToEnd();
        }

    }
}
