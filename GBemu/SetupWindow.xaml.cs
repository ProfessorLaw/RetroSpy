﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using System.Xml.Linq;

namespace GBPemu
{

    public class COMPortInfo
    {
        public string PortName { get; set; }
        public string FriendlyName { get; set; }
    }

    public class ListView<T>
    {
        private readonly List<T> _items;

        public CollectionView Items { get; private set; }
        public T SelectedItem { get; set; }

        public ListView()
        {
            _items = new List<T>();
            Items = new CollectionView(_items);
        }

        public void UpdateContents(IEnumerable<T> items)
        {
            _items.Clear();
            _items.AddRange(items);

            if (Items.Dispatcher.CheckAccess())
            {
                Items.Refresh();
            }
            else
            {
                Items.Dispatcher.Invoke(() =>
                {
                    Items.Refresh();
                });
            }
        }

        public void SelectFirst()
        {
            if (_items.Count > 0)
            {
                SelectedItem = _items[0];
            }
        }

        public void SelectId(int id)
        {
            if (_items.Count > 0 && id >= 0 && id < _items.Count)
            {
                SelectedItem = _items[id];
            }
            else
            {
                SelectFirst();
            }
        }

        public void SelectIdFromText(T text)
        {
            int index = _items.IndexOf(text);
            SelectId(index);
        }

        public int GetSelectedId()
        {
            return SelectedItem != null ? _items.IndexOf(SelectedItem) : -1;
        }
    }

    public partial class SetupWindow : Window
    {
        private readonly SetupWindowViewModel _vm;
        private readonly DispatcherTimer _portListUpdateTimer;
        private readonly ResourceManager _resources;
        private bool isClosing;

        private List<string> arduinoPorts;
        private void UpdatePortListThread()
        {
            Thread thread = new Thread(UpdatePortList);
            thread.Start();
        }

        public SetupWindow()
        {
            InitializeComponent();

            if (Properties.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }


            isClosing = false;
            _vm = new SetupWindowViewModel();
            DataContext = _vm;
            _resources = Properties.Resources.ResourceManager;
            string strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string strWorkPath = Path.GetDirectoryName(strExeFilePath);

            _vm.FilterCOMPorts = Properties.Settings.Default.FilterCOMPorts;

            MenuItem menuItem = new MenuItem
            {
                Header = "COM Ports"
            };

            COMMenu = menuItem;

            if (_vm.FilterCOMPorts)
            {
                OptionsMenu.Items.Insert(OptionsMenu.Items.Count, COMMenu);
            }
            else
            {
                firstTime = true;
            }

            _portListUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _portListUpdateTimer.Tick += (sender, e) => UpdatePortListThread();
            _portListUpdateTimer.Start();

            UpdatePortList();
            _vm.Ports.SelectIdFromText(Properties.Settings.Default.Port);

            //GoButton_Click(null, null);
        }

        bool firstTime = false;
        MenuItem COMMenu;

        private void FilterCOM_Checked(object sender, RoutedEventArgs e)
        {
            if (!firstTime)
            {
                firstTime = true;
                return;
            }
            _vm.FilterCOMPorts = FilterCOM.IsChecked;
            Properties.Settings.Default.FilterCOMPorts = FilterCOM.IsChecked;

            MenuItem menuItem = new MenuItem
            {
                Header = "COM Ports"
            };

            COMMenu = menuItem;

            OptionsMenu.Items.Insert(OptionsMenu.Items.Count, COMMenu);
        }

        private void FilterCOM_Unchecked(object sender, RoutedEventArgs e)
        {
            _vm.FilterCOMPorts = FilterCOM.IsChecked;
            Properties.Settings.Default.FilterCOMPorts = FilterCOM.IsChecked;

            OptionsMenu.Items.Remove(COMMenu);
        }

        private void COMPortClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                string port = ((MenuItem)sender).Header.ToString();
                using (SerialPort _serialPort = new SerialPort(port, 115200, Parity.None, 8, StopBits.One)
                {
                    Handshake = Handshake.None,
                    ReadTimeout = 500,
                    WriteTimeout = 500
                })
                {
                    _serialPort.Open();
                    _serialPort.Write("\x88\x33\x0F\x00\x00\x00\x0F\x00\x00");
                    string result = null;
                    do
                    {
                        result = _serialPort.ReadLine();
                    } while (result != null && (result.StartsWith("!", StringComparison.Ordinal) || result.StartsWith("#", StringComparison.Ordinal)));

                    if (result == "parse_state:0\r" || result.Contains("d=debug"))
                    {
                        _serialPort.Close();
                        Thread.Sleep(1000);

                        if (Dispatcher.CheckAccess())
                        {
                            Hide();
                        }
                        else
                        {
                            Dispatcher.Invoke(() =>
                            {
                                Hide();
                            });
                        }

                        Properties.Settings.Default.Port = _vm.Ports.SelectedItem;
                        Properties.Settings.Default.FilterCOMPorts = _vm.FilterCOMPorts;
                        Properties.Settings.Default.Save();

                        try
                        {
                            if (Dispatcher.CheckAccess())
                            {
                                IControllerReader reader = InputSource.PRINTER.BuildReader(port);
                                _ = new GameBoyPrinterEmulatorWindow(reader).ShowDialog();
                            }
                            else
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    IControllerReader reader = InputSource.PRINTER.BuildReader(port);
                                    _ = new GameBoyPrinterEmulatorWindow(reader).ShowDialog();
                                });
                            }

                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            _ = MessageBox.Show(ex.Message, _resources.GetString("RetroSpy", CultureInfo.CurrentUICulture), MessageBoxButton.OK, MessageBoxImage.Error);
                        }

                        if (Dispatcher.CheckAccess())
                        {
                            Show();
                        }
                        else
                        {
                            Dispatcher.Invoke(() =>
                            {
                                Show();
                            });
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                _ = MessageBox.Show(ex.Message, _resources.GetString("RetroSpy", CultureInfo.CurrentUICulture), MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (Dispatcher.CheckAccess())
            {
                Show();
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    Show();
                });
            }

        }

        private readonly object updatePortLock = new object();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "I am fishing for the printer, so I expect failures.")]
        private void UpdatePortList()
        {
            if (!isClosing && Monitor.TryEnter(updatePortLock))
            {
                try
                {
                    arduinoPorts = SetupCOMPortInformation();

                    if (!_vm.FilterCOMPorts)
                    {
                        foreach (string port in arduinoPorts)
                        {
                            using (SerialPort _serialPort = new SerialPort(port, 115200, Parity.None, 8, StopBits.One)
                            {
                                Handshake = Handshake.None,
                                ReadTimeout = 500,
                                WriteTimeout = 500
                            })
                            {

                                try
                                {
                                    _serialPort.Open();
                                }
                                catch (Exception)
                                {
                                    continue;
                                }

                                try
                                {
                                    _serialPort.Write("\x88\x33\x0F\x00\x00\x00\x0F\x00\x00");
                                }
                                catch (Exception)
                                {
                                    _serialPort.Close();
                                    continue;
                                }

                                try
                                {
                                    string result = null;
                                    do
                                    {
                                        result = _serialPort.ReadLine();
                                    } while (result != null && (result.StartsWith("!", StringComparison.Ordinal) || result.StartsWith("#", StringComparison.Ordinal)));

                                    if (result == "parse_state:0\r" || result.Contains("d=debug"))
                                    {
                                        _serialPort.Close();
                                        Thread.Sleep(1000);

                                        if (Dispatcher.CheckAccess())
                                        {
                                            Hide();
                                        }
                                        else
                                        {
                                            Dispatcher.Invoke(() =>
                                            {
                                                Hide();
                                            });
                                        }

                                        Properties.Settings.Default.Port = _vm.Ports.SelectedItem;
                                        Properties.Settings.Default.FilterCOMPorts = _vm.FilterCOMPorts;
                                        Properties.Settings.Default.Save();

                                        try
                                        {
                                            if (Dispatcher.CheckAccess())
                                            {
                                                IControllerReader reader = InputSource.PRINTER.BuildReader(port);
                                                _ = new GameBoyPrinterEmulatorWindow(reader).ShowDialog();
                                            }
                                            else
                                            {
                                                Dispatcher.Invoke(() =>
                                                {
                                                    IControllerReader reader = InputSource.PRINTER.BuildReader(port);
                                                    _ = new GameBoyPrinterEmulatorWindow(reader).ShowDialog();
                                                });
                                            }


                                        }
                                        catch (UnauthorizedAccessException ex)
                                        {
                                            _ = MessageBox.Show(ex.Message, _resources.GetString("RetroSpy", CultureInfo.CurrentUICulture), MessageBoxButton.OK, MessageBoxImage.Error);
                                        }

                                        if (Dispatcher.CheckAccess())
                                        {
                                            Show();
                                        }
                                        else
                                        {
                                            Dispatcher.Invoke(() =>
                                            {
                                                Show();
                                            });
                                        }

                                    }
                                    else
                                    {
                                        _serialPort.Close();
                                        continue;
                                    }
                                }
                                catch (Exception)
                                {
                                    _serialPort.Close();
                                    continue;
                                }

                            }
                        }
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke((Action)delegate
                        {
                            if (COMMenu.Items.Count != arduinoPorts.Count)
                            {
                                COMMenu.Items.Clear();
                                foreach (var port in arduinoPorts)
                                {
                                    var newMenuItem = new MenuItem();
                                    newMenuItem.Header = port;
                                    newMenuItem.Click += COMPortClicked;
                                    COMMenu.Items.Insert(COMMenu.Items.Count, newMenuItem);
                                }
                            }
                        });
                    }

                }
                catch (TaskCanceledException)
                {
                    // Closing the window can cause this due to a race condition
                }
                finally
                {
                    Monitor.Exit(updatePortLock);
                }
            }
        }

        private static string[] GetUSBCOMDevices()
        {
            List<string> list = new List<string>();

            ManagementObjectSearcher searcher2 = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity");
            foreach (ManagementObject mo2 in searcher2.Get())
            {
                if (mo2["Name"] != null)
                {
                    string name = mo2["Name"].ToString();
                    // Name will have a substring like "(COM12)" in it.
                    if (name.Contains("(COM"))
                    {
                        list.Add(name);
                    }
                }
            }
            searcher2.Dispose();
            // remove duplicates, sort alphabetically and convert to array
            string[] usbDevices = list.Distinct().OrderBy(s => s).ToArray();
            return usbDevices;
        }

        private List<string> SetupCOMPortInformation()
        {
            List<COMPortInfo> comPortInformation = new List<COMPortInfo>();

            string[] portNames = SerialPort.GetPortNames();
            foreach (string s in portNames)
            {
                // s is like "COM14"
                COMPortInfo ci = new COMPortInfo
                {
                    PortName = s,
                    FriendlyName = s
                };
                comPortInformation.Add(ci);
            }

            string[] usbDevs = GetUSBCOMDevices();
            foreach (string s in usbDevs)
            {
                // Name will be like "USB Bridge (COM14)"
                int start = s.IndexOf("(COM", StringComparison.Ordinal) + 1;
                if (start >= 0)
                {
                    int end = s.IndexOf(")", start + 3, StringComparison.Ordinal);
                    if (end >= 0)
                    {
                        // cname is like "COM14"
                        string cname = s.Substring(start, end - start);
                        for (int i = 0; i < comPortInformation.Count; i++)
                        {
                            if (comPortInformation[i].PortName == cname)
                            {
                                comPortInformation[i].FriendlyName = s.Remove(start - 1).TrimEnd();
                            }
                        }
                    }
                }
            }

            List<string> ports = new List<string>();
            foreach (COMPortInfo port in comPortInformation)
            {
                if (_vm.FilterCOMPorts || port.FriendlyName.Contains("Arduino"))
                {
                    ports.Add(port.PortName);
                }
                else if (port.FriendlyName.Contains("CH340") || port.FriendlyName.Contains("CH341"))
                {
                    ports.Add(port.PortName);
                }
            }

            return ports;
        }

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            Properties.Settings.Default.Port = _vm.Ports.SelectedItem;
            Properties.Settings.Default.FilterCOMPorts = _vm.FilterCOMPorts;
            Properties.Settings.Default.Save();

            IControllerReader reader = InputSource.PRINTER.BuildReader("COM16");

            try
            {
                _ = new GameBoyPrinterEmulatorWindow(reader).ShowDialog();
            }
            catch (UnauthorizedAccessException ex)
            {
                _ = MessageBox.Show(ex.Message, _resources.GetString("RetroSpy", CultureInfo.CurrentUICulture), MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Show();
        }

        private void ComPortCombo_SelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdatePortList();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            isClosing = true;
        }
    }

    public class SetupWindowViewModel : INotifyPropertyChanged
    {
        public ListView<string> Ports { get; set; }
        public bool FilterCOMPorts { get; set; }

        private Visibility _comPortOptionVisibility;

        public Visibility ComPortOptionVisibility
        {
            get => _comPortOptionVisibility;
            set
            {
                _comPortOptionVisibility = value;
                NotifyPropertyChanged("ComPortOptionVisibility");
            }
        }


        public SetupWindowViewModel()
        {
            Ports = new ListView<string>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string prop)
        {
            if (PropertyChanged == null)
            {
                return;
            }

            PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }

    }
}