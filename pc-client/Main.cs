﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;
using System.Globalization;

namespace pc_client
{
    public partial class MainForm : Form
    {
        ///////////////////////////////////////////////////////////////////////
        #region Variables

        ComWrapper _comWrapper = null;
        Dispatcher _dispatcher = null;
        Helper _helper = null;
        Simulator _simulator = null;
        ErrorMessageBoxes _error = null;
        Settings _settings = null;
        Data _data = null;
        int _waitCounter = 1;
        Object _lockObject = new Object();
        volatile bool _simulateResponses = false;
        volatile bool _stopReceivingEepromData = false;
        double _range = 0;

        List<string> _commandHistory = new List<string>();

        System.Windows.Threading.Dispatcher _windowDispatcher;
        Form _receiverForm = null;
       

        delegate void NewDataReceivedDelegate(object sender, byte[] data);

        #endregion


        ///////////////////////////////////////////////////////////////////////
        #region Initialization

        public MainForm()
        {
            _comWrapper = new ComWrapper();
            _dispatcher = new Dispatcher(this, _comWrapper);
            _data = Data.GetInstance;
            _helper = new Helper();
            _simulator = new Simulator(_dispatcher);
            _settings = Settings.GetInstance;
            _error = new ErrorMessageBoxes();
            _windowDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            _receiverForm = this;

            InitializeComponent();
            InitializeControlValues();
            EnableControls();
            InitializePortTimer();
            RegisterEventHandlers(true);
        }


        public void InitializeControlValues()
        {
            cmbParity.Items.Clear();
            cmbParity.Items.AddRange(Enum.GetNames(typeof(Parity)));
            cmbStopBits.Items.Clear();
            cmbStopBits.Items.AddRange(Enum.GetNames(typeof(StopBits)));
            cmbParity.Text = Properties.Settings.Default.Parity;
            cmbStopBits.Text = Properties.Settings.Default.StopBits;
            cmbDataBits.Text = Properties.Settings.Default.DataBits;
            cmbBaudRate.Text = Properties.Settings.Default.BaudRate;
            chkDTR.Checked = Properties.Settings.Default.DTR;
            chkRTS.Checked = Properties.Settings.Default.RTS;
            cmbPortName.Text = Properties.Settings.Default.Port;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////
        #region EventHandlers

        private void RegisterEventHandlers(bool register)
        {
            if (register)
            {
                _dispatcher.NewHardwareDataReceivedEvent += new Dispatcher.NewHardwareDataReceivedEventHandler(Dispatcher_NewHardwareDataReceivedEvent);
                _dispatcher.NewTemperatureDataReceivedEvent += new Dispatcher.NewTemperatureDataReceivedEventHandler(Dispatcher_NewTemperatureDataReceivedEvent);
                _dispatcher.NewADChannel1DataReceivedEvent += new Dispatcher.NewADChannel1DataReceivedEventHandler(Dispatcher_NewADChannel1DataReceivedEvent);
                _dispatcher.NewHardwareDataReceivedEvent += new Dispatcher.NewHardwareDataReceivedEventHandler(Dispatcher_NewHardwareDataReceivedEvent);
                _dispatcher.NewADChannel2DataReceivedEvent += new Dispatcher.NewADChannel2DataReceivedEventHandler(Dispatcher_NewADChannel2DataReceivedEvent);
                _dispatcher.NewErrorReceivedEvent += new Dispatcher.NewErrorReceivedEventHandler(Dispatcher_NewErrorReceivedEvent);
                _dispatcher.NewEpromDataReceivedEvent += new Dispatcher.NewEpromDataReceivedEventHandler(Dispatcher_NewEpromDataReceivedEvent);
                _dispatcher.NewTerminalDataReceivedEvent += new Dispatcher.NewTerminalDataReceivedEventHandler(Dispatcher_NewTerminalDataReceivedEvent);
                _dispatcher.NewVoidDataReceivedEvent += new Dispatcher.NewVoidDataReceivedEventHandler(Dispatcher_NewVoidDataReceivedEvent);
                _dispatcher.NewLogOutputDataReceivedEvent += new Dispatcher.NewLogOutputDataReceivedEventHandler(Dispatcher_NewLogOutputDataReceivedEvent);
                _dispatcher.NewLogInputDataReceivedEvent += new Dispatcher.NewLogInputDataReceivedEventHandler(Dispatcher_NewLogInputDataReceivedEvent);
            }
            else
            {
                _dispatcher.NewHardwareDataReceivedEvent -= new Dispatcher.NewHardwareDataReceivedEventHandler(Dispatcher_NewHardwareDataReceivedEvent);
                _dispatcher.NewTemperatureDataReceivedEvent -= new Dispatcher.NewTemperatureDataReceivedEventHandler(Dispatcher_NewTemperatureDataReceivedEvent);
                _dispatcher.NewADChannel1DataReceivedEvent -= new Dispatcher.NewADChannel1DataReceivedEventHandler(Dispatcher_NewADChannel1DataReceivedEvent);
                _dispatcher.NewHardwareDataReceivedEvent -= new Dispatcher.NewHardwareDataReceivedEventHandler(Dispatcher_NewHardwareDataReceivedEvent);
                _dispatcher.NewADChannel2DataReceivedEvent -= new Dispatcher.NewADChannel2DataReceivedEventHandler(Dispatcher_NewADChannel2DataReceivedEvent);
                _dispatcher.NewErrorReceivedEvent -= new Dispatcher.NewErrorReceivedEventHandler(Dispatcher_NewErrorReceivedEvent);
                _dispatcher.NewEpromDataReceivedEvent -= new Dispatcher.NewEpromDataReceivedEventHandler(Dispatcher_NewEpromDataReceivedEvent);
                _dispatcher.NewTerminalDataReceivedEvent -= new Dispatcher.NewTerminalDataReceivedEventHandler(Dispatcher_NewTerminalDataReceivedEvent);
                _dispatcher.NewVoidDataReceivedEvent -= new Dispatcher.NewVoidDataReceivedEventHandler(Dispatcher_NewVoidDataReceivedEvent);
                _dispatcher.NewLogOutputDataReceivedEvent -= new Dispatcher.NewLogOutputDataReceivedEventHandler(Dispatcher_NewLogOutputDataReceivedEvent);
                _dispatcher.NewLogInputDataReceivedEvent -= new Dispatcher.NewLogInputDataReceivedEventHandler(Dispatcher_NewLogInputDataReceivedEvent);
            }
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////
        #region Receivers

        void Dispatcher_NewHardwareDataReceivedEvent(object sender, byte[] value)
        {
            if(value != null && value.Length != 0)
            {
                byte first4Bit = unchecked((byte)(value[0] >> 4));
                byte last4Bit = unchecked((byte)(value[0] & 15));
                String hardware = _helper.HexToString(first4Bit) + "." + _helper.HexToString(last4Bit);
                tbHardware.Text = hardware;
                StopBGWTimer();
            }
        }


        void Dispatcher_NewTemperatureDataReceivedEvent(object sender, byte[] value)
        {
            if(value != null)
            {
                _data.ADT_Raw += _helper.HexArrayToString(value);
            }

            if(_data.ADT_Raw.Length == 4)
            { 
                try
                {
                    int temp = _helper.HexStringToDecimal(_data.ADT_Raw);
                    _data.Temperature = Commands.calculateTemperature(temp);
                    tbTemperatur.Text = _data.Temperature.ToString("F3", CultureInfo.InvariantCulture);
                    StopBGWTimer();
                }
                catch (Exception)
                {
                    StopBGWTimer();
                }
            }
        }


        void Dispatcher_NewADChannel1DataReceivedEvent(object sender, byte[] value)
        {
            if(value != null)
            {
                _data.ADW1_Raw += _helper.HexArrayToString(value);
            }

            if(_data.ADW1_Raw.Length == 6)
            { 
                try
                {
                    _data.ADW1 = Commands.calculateVoltage(_range, _helper.HexStringToDecimal(_data.ADW1_Raw));
                    tbADChannel1.Text = _data.ADW1.ToString("F5", CultureInfo.InvariantCulture);
                    StopBGWTimer();
                }
                catch (Exception)
                {
                    StopBGWTimer();
                }
            }
        }


        void Dispatcher_NewADChannel2DataReceivedEvent(object sender, byte[] value)
        {
            if(value != null)
            {
                _data.ADW2_Raw += _helper.HexArrayToString(value);
            }

            if(_data.ADW2_Raw.Length == 6)
            { 
                try
                {
                    _data.ADW2 = Commands.calculateVoltage(_range, _helper.HexStringToDecimal(_data.ADW2_Raw));
                    tbADChannel2.Text = _data.ADW2.ToString("F5", CultureInfo.InvariantCulture);
                    StopBGWTimer();
                }
                catch (Exception)
                {
                    StopBGWTimer();
                }
            }
        }


        void Dispatcher_NewEpromDataReceivedEvent(object sender, byte[] value)
        {
            if (_dispatcher.EepromIsReceivingEmptyData() || Array.Exists(value, element => element == Commands.EMPTY))
            {
                _stopReceivingEepromData = true;
            }
            if (value != null && !_stopReceivingEepromData)
            {
                _data.Eprom += System.Text.Encoding.Default.GetString(value);
                rtfEprom.Text = _data.Eprom;
                StopBGWTimer();
            }
        }


        void Dispatcher_NewTerminalDataReceivedEvent(object sender, byte[] data)
        {
            StopBGWTimer();
            _data.AppendTerminalData(data);

            if (chkInputType.Checked == true)
            {
                rtfTerminalIn.Clear();

                rtfTerminalIn.AppendText(_helper.HexToString(_data.TerminalData[0]));
                for (int i = 1; i < _data.TerminalData.Length; i++)
                {
                    rtfTerminalIn.AppendText(":");
                    rtfTerminalIn.AppendText(_helper.HexToString(_data.TerminalData[i]));
                }
            }
            else
            {
                rtfTerminalIn.Text = System.Text.Encoding.Default.GetString(_data.TerminalData);
            }
        }


        void Dispatcher_NewErrorReceivedEvent(object sender, string text)
        {
            _error.SendingCommandError(text);
            StopBGWTimer();
        }


        void Dispatcher_NewLogInputDataReceivedEvent(object sender, byte[] value)
        {
            if (value != null)
            {
                logInput(value);
            }

            if (_simulateResponses == true)
            {
                _simulator.SimulateResponses(value[0]);
            }
        }


        void Dispatcher_NewLogOutputDataReceivedEvent(object sender, String value)
        {
            if (value != null)
            {
                logOutput(value);
            }
        }


        void Dispatcher_NewVoidDataReceivedEvent(object sender, byte[] value)
        {
            StopBGWTimer();
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////
        #region SaveSettings

        public void SaveSettings()
        {
            _settings.PortName = cmbPortName.Text;
            _settings.Parity = cmbParity.Text;
            _settings.StopBits = cmbStopBits.Text;
            _settings.DataBits = cmbDataBits.Text;
            _settings.BaudRate = cmbBaudRate.Text;
            _settings.DTR = chkDTR.Checked;
            _settings.RTS = chkRTS.Checked;

            Properties.Settings.Default.Port = cmbPortName.Text;
            Properties.Settings.Default.BaudRate = cmbBaudRate.Text;
            Properties.Settings.Default.DataBits = cmbDataBits.Text;
            Properties.Settings.Default.Parity = cmbParity.Text;
            Properties.Settings.Default.StopBits = cmbStopBits.Text;
            Properties.Settings.Default.RTS = chkRTS.Checked;
            Properties.Settings.Default.DTR = chkDTR.Checked;

            Properties.Settings.Default.Save();
        }

        #endregion
                

        ///////////////////////////////////////////////////////////////////////
        #region ClosePort

        public void close_port()
        {
            KillAllBackGroundActions();
            _comWrapper.ComportDispose();
        }

        private void KillAllBackGroundActions()
        {
            _backgroundWorkerHardware.CancelAsync();
            _backgroundWorkerHardware.Dispose();
            _backgroundWorkerTemperature.CancelAsync();
            _backgroundWorkerTemperature.Dispose();
            _backgroundWorkerADW.CancelAsync();
            _backgroundWorkerADW.Dispose();
            _backgroundWorkerADW2.CancelAsync();
            _backgroundWorkerADW2.Dispose();
            _backgroundWorkerEepromRead.CancelAsync();
            _backgroundWorkerEepromRead.Dispose();
            _backgroundWorkerEepromWrite.CancelAsync();
            _backgroundWorkerEepromWrite.Dispose();
            _backgroundWorkerRange.CancelAsync();
            _backgroundWorkerRange.Dispose();

            StopBGWTimer();
            tempTimer.Stop();
            tempTimer.Enabled = false;
            this.tempTimer.Tick -= new System.EventHandler(this.tempTimer_Tick);
            ad1Timer.Stop();
            ad1Timer.Enabled = false;
            this.ad1Timer.Tick -= new System.EventHandler(this.ad1Timer_Tick);
            ad2Timer.Stop();
            ad2Timer.Enabled = false;
            this.ad2Timer.Tick -= new System.EventHandler(this.ad2Timer_Tick);
        }

        #endregion
        

        ///////////////////////////////////////////////////////////////////////
        #region OpenPort
        
        private void btnOpenPort_Click(object sender, EventArgs e)
        {
            if (btnOpenPort.Text == "Open Port")
            {
                open_port();
                if(_comWrapper.ComportIsOpen() == true)
                {
                    btnOpenPort.Text = "Close Port";
                    btnConnect.Text = "Close";
                    DisableSettingsControls();
                }
            }
            else if (btnOpenPort.Text == "Close Port")
            {
                close_port();
                if(_comWrapper.ComportIsOpen() == false)
                {
                    btnOpenPort.Text = "Open Port";
                    btnConnect.Text = "Connect";
                    EnableSettingsControls();
                }
            }

            rtfTerminalOut.Focus();
        }
        

        public void open_port()
        {
            bool error = false;

            try
            {
                SaveSettings();

                // Open the port
                _comWrapper.ComportOpen();                    
            }

            catch (UnauthorizedAccessException) { error = true; }
            catch (IOException) { error = true; }
            catch (ArgumentException) { error = true; }

            if (error) 
            {
               MessageBox.Show(this, "Could not open the COM port.  Most likely it is already in use, has been removed, or is unavailable.", "COM Port Unavalible", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }

                DisableSettingsControls();

            // Change the state of the form's controls
            EnableControls();
        }

        #endregion
        

        ///////////////////////////////////////////////////////////////////////
        #region RefreshComportList

        private void RefreshComPortList()
        {
            // Determain if the list of com port names has changed since last checked
            string selected = RefreshComPortList(cmbPortName.Items.Cast<string>(), cmbPortName.SelectedItem as string, _comWrapper.ComportIsOpen());

            // If there was an update, then update the control showing the user the list of port names
            if (!String.IsNullOrEmpty(selected))
            {
                cmbPortName.Items.Clear();
                cmbPortName.Items.AddRange(OrderedPortNames());
                cmbPortName.SelectedItem = selected;
            }
        }
        

        private string[] OrderedPortNames()
        {
            // Just a placeholder for a successful parsing of a string to an integer
            int num;

            // Order the serial port names in numberic order (if possible)
            return _comWrapper.GetAvailablePorts().OrderBy(a => a.Length > 3 && int.TryParse(a.Substring(3), out num) ? num : 0).ToArray();
        }
        

        private string RefreshComPortList(IEnumerable<string> PreviousPortNames, string CurrentSelection, bool PortOpen)
        {
            // Create a new return report to populate
            string selected = null;

            // Retrieve the list of ports currently mounted by the operating system (sorted by name)
            string[] ports = _comWrapper.GetAvailablePorts();

            // First determain if there was a change (any additions or removals)
            bool updated = PreviousPortNames.Except(ports).Count() > 0 || ports.Except(PreviousPortNames).Count() > 0;

            // If there was a change, then select an appropriate default port
            if (updated)
            {
                // Use the correctly ordered set of port names
                ports = OrderedPortNames();

                // Find newest port if one or more were added
                string newest = _comWrapper.GetAvailablePorts().Except(PreviousPortNames).OrderBy(a => a).LastOrDefault();

                // If the port was already open... (see logic notes and reasoning in Notes.txt)
                if (PortOpen)
                {
                    if (ports.Contains(CurrentSelection)) selected = CurrentSelection;
                    else if (!String.IsNullOrEmpty(newest)) selected = newest;
                    else selected = ports.LastOrDefault();
                }
                else
                {
                    if (!String.IsNullOrEmpty(newest)) selected = newest;
                    else if (ports.Contains(CurrentSelection)) selected = CurrentSelection;
                    else selected = ports.LastOrDefault();
                }
            }

            // If there was a change to the port list, return the recommended default selection
            return selected;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////
        #region BackgroundWorkers

        void BackgroundWorkerHardware_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                WaitForPendingBytes();
                lock (_lockObject)
                {
                    _dispatcher.SetPendingBytes(2);
                    _dispatcher.SendData(Commands.ID_HARDWARE, (char)(Commands.MASTER | Commands.READ));
                }
            }
            catch
            {
                _error.FatalError();
            }
        }


        private void _backgroundWorkerHardware_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            DisableSettingsControls();
        }


        void BackgroundWorkerRange_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                char command = (char)e.Argument;
                WaitForPendingBytes();
                lock (_lockObject)
                {
                    _dispatcher.SetPendingBytes(1);
                    _dispatcher.SendData(Commands.ID_RANGE, command);
                }
            }
            catch
            {
                _error.FatalError();
            }
        }


        private void _backgroundWorkerRange_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            DisableSettingsControls();
        }


        void BackgroundWorkerTemperature_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                WaitForPendingBytes();
                lock (_lockObject)
                {
                    _dispatcher.SetPendingBytes(3);
                    _dispatcher.SendData(Commands.ID_TEMPERATURE, (char)(Commands.ADT | Commands.READ));
                }
            }
            catch
            {
                _error.FatalError();
            }
        }


        private void _backgroundWorkerTemperature_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            DisableSettingsControls();
        }


        private void _backgroundWorkerADW_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                WaitForPendingBytes();
                lock (_lockObject)
                {
                    _dispatcher.SetPendingBytes(1);
                    _dispatcher.SendData(Commands.ID_ADCHANNEL1, (char)(Commands.ADW | Commands.CH1));
                }
                WaitForPendingBytes();
                lock (_lockObject)
                {
                    _dispatcher.SetPendingBytes(4);
                    _dispatcher.SendData(Commands.ID_ADCHANNEL1, (char)(Commands.ADW | Commands.READ));
                }
            }
            catch
            {
                _error.FatalError();
            }
        }


        private void _backgroundWorkerADW_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            DisableSettingsControls();
        }


        private void _backgroundWorkerADW2_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            DisableSettingsControls();
        }


        private void _backgroundWorkerADW2_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                WaitForPendingBytes();
                lock (_lockObject)
                {
                    _dispatcher.SetPendingBytes(1);
                    _dispatcher.SendData(Commands.ID_ADCHANNEL2, (char)(Commands.ADW | Commands.CH2));
                }
                WaitForPendingBytes();
                lock (_lockObject)
                {
                    _dispatcher.SetPendingBytes(4);
                    _dispatcher.SendData(Commands.ID_ADCHANNEL2, (char)(Commands.ADW | Commands.READ));
                }
            }
            catch
            {
                _error.FatalError();
            }
        }


        private void _backgroundWorkerEepromRead_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                for (int i = 0x0; i < 0x200; i++)
                {
                    if(_dispatcher.EepromIsReceivingEmptyData() == true)
                    {
                        _dispatcher.SetReceivingEmptyData(false);
                        return;
                    }

                    WaitForPendingBytes();
                    lock (_lockObject)
                    {
                        byte[] sendBytes = { (Commands.EEPROM | Commands.READ), unchecked((byte)(i >> 8)), unchecked((byte)(i << 256))};
                        _dispatcher.SetPendingBytes(2);
                        _dispatcher.SendData(Commands.ID_EEPROM_READ, sendBytes);
                    }
                }
            }
            catch
            {
                _error.FatalError();
            }
        }


        private void _backgroundWorkerEepromRead_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            _dispatcher.SetReceivingEmptyData(false);
            DisableSettingsControls();
        }


        private void _backgroundWorkerEepromWrite_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                String sendString = _data.Eprom;
                int lastAdress = 0;
                for (int i = 0x0; i < Math.Min(0x200, sendString.Length); i++)
                {
                    WaitForPendingBytes();
                    lock (_lockObject)
                    {
                        byte[] sendingBytes = { (Commands.EEPROM | Commands.WRITE), unchecked((byte)(i >> 8)), unchecked((byte)(i << 256)), (byte)sendString[i] };
                        _dispatcher.SetPendingBytes(2);
                        _dispatcher.SendData(Commands.ID_EEPROM_WRITE, sendingBytes);
                        lastAdress = i + 1;
                    }
                }

                WaitForPendingBytes();
                lock (_lockObject)
                {
                    if(lastAdress == 512)
                    {
                        lastAdress = 511;
                    }
                    byte[] sendBytes = { (Commands.EEPROM | Commands.WRITE), unchecked((byte)(lastAdress >> 8)), unchecked((byte)(lastAdress << 256)), 0xff };
                    _dispatcher.SetPendingBytes(2);
                    _dispatcher.SendData(Commands.ID_EEPROM_WRITE, sendBytes);
                }
            }
            catch
            {
                _error.FatalError();
            }
        }


        private void _backgroundWorkerEepromWrite_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            DisableSettingsControls();
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////
        #region Sending-Timer

        private void InitializeBGWTimer()
        {
            bgwTimer.Interval = 5000;
            bgwTimer.Enabled = true;
            this.bgwTimer.Tick += new System.EventHandler(this.bgwTimer_Tick);
        }


        private void StopBGWTimer()
        {
            bgwTimer.Stop();
            bgwTimer.Enabled = false;
            this.bgwTimer.Tick -= new System.EventHandler(this.bgwTimer_Tick);
        }


        private void bgwTimer_Tick(object sender, System.EventArgs e)
        {
            StopBGWTimer();
            KillAllBackGroundActions();
            _error.TimeOutError(_dispatcher.GetSendCommand());
            DisableSettingsControls();
            _dispatcher.SetPendingBytes(0);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////
        #region EnableSettingsControls

        public void EnableControls()
        {
            if (_comWrapper.ComportIsOpen())
            {
                DisableSettingsControls();
                btnOpenPort.Text = "Close Port";
            }
            else
            {
                EnableSettingsControls();
                btnOpenPort.Text = "Open Port";
            }
        }


        public void EnableSettingsControls()
        {
            chkDTR.Enabled = true;
            chkRTS.Enabled = true;
            cmbPortName.Enabled = true;
            cmbParity.Enabled = true;
            cmbBaudRate.Enabled = true;
            cmbStopBits.Enabled = true;
            cmbDataBits.Enabled = true;
            chkSimulation.Enabled = true;

            btnReadADC1.Enabled = false;
            btnReadADC2.Enabled = false;
            btnReadEprom.Enabled = false;
            btnReadTemperatur.Enabled = false;
            btnResetHardware.Enabled = false;
            btnWriteEprom.Enabled = false;
            chkPollTemp.Enabled = false;
            chkAD1.Enabled = false;
            chkAD2.Enabled = false;
            rbV1.Enabled = false;
            rbV2.Enabled = false;
            btnSend.Enabled = false;
        }


        public void DisableSettingsControls()
        {
            chkDTR.Enabled = false;
            chkRTS.Enabled = false;
            cmbPortName.Enabled = false;
            cmbParity.Enabled = false;
            cmbBaudRate.Enabled = false;
            cmbStopBits.Enabled = false;
            cmbDataBits.Enabled = false;

            if(rbV1.Checked == false && rbV2.Checked == false)
            {
                btnReadADC1.Enabled = false;
                btnReadADC2.Enabled = false;
                chkAD1.Enabled = false;
                chkAD2.Enabled = false;
            }
            else
            {
                btnReadADC1.Enabled = true;
                btnReadADC2.Enabled = true;
                chkAD1.Enabled = true;
                chkAD2.Enabled = true;
            }

            rtfTerminalOut.Enabled = true;
            btnReadEprom.Enabled = true;
            btnReadTemperatur.Enabled = true;
            btnResetHardware.Enabled = true;
            btnWriteEprom.Enabled = true;
            chkPollTemp.Enabled = true;
            rbV1.Enabled = true;
            rbV2.Enabled = true;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////
        #region Comport-Tab-InputEvents

        private void cmbPortName_DropDown(object sender, EventArgs e)
        {
            RefreshComPortList();
        }


        private void cmbBaudRate_DropDown(object sender, EventArgs e)
        {
            cmbBaudRate.Items.Clear();
            cmbBaudRate.Items.AddRange(_comWrapper.GetBaudRateRange());          
        }


        private void cmbDataBits_DropDown(object sender, EventArgs e)
        {
            cmbDataBits.Items.Clear();
            cmbDataBits.Items.AddRange(_comWrapper.GetDataBitsRange());   
        }
        

        private void OnMouseEntering(object sender, EventArgs e)
        {
            rtfTerminalOut.Focus();
        }


        private void btnSend_Click(object sender, EventArgs e)
        {
            TerminalSendData();
        }


        private void rtfTerminalOut_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == (char)13)
            {
                TerminalSendData();
            }
        }


        private void btnClearOut_Click(object sender, EventArgs e)
        {
            rtfTerminalOut.Clear();
            btnClearOut.Enabled = false;
        }


        private void btnClearIn_Click(object sender, EventArgs e)
        {
            rtfTerminalIn.Clear();
            btnClearIn.Enabled = false;
            _data.TerminalData = new byte[0];
        }


        private void chkInputType_CheckedChanged(object sender, EventArgs e)
        {
            if (_data.TerminalData.Length == 0)
            {
                return; // nothing to convert
            }

            rtfTerminalIn.Clear();

            if (chkInputType.Checked == true)
            {
                rtfTerminalIn.AppendText(_helper.HexToString(_data.TerminalData[0]));
                for (int i = 1; i < _data.TerminalData.Length; i++)
                {
                    rtfTerminalIn.AppendText(":");
                    rtfTerminalIn.AppendText(_helper.HexToString(_data.TerminalData[i]));
                }
            }
            else
            {
                rtfTerminalIn.AppendText(System.Text.Encoding.Default.GetString(_data.TerminalData));
            }
        }

        private void rtfTerminalOut_TextChanged(object sender, EventArgs e)
        {
            if (rtfTerminalOut.Text.Length > 0)
            {
                btnClearOut.Enabled = true;
            }

            if (rtfTerminalOut.Lines.Length == 0)
            {
                btnSend.Enabled = false;
                btnClearOut.Enabled = false;
                return; //nothing left to do
            }

            if (rtfTerminalOut.Lines[rtfTerminalOut.Lines.Length - 1].Length > 0)
            {
                btnSend.Enabled = true;
            }
            else
            {
                btnSend.Enabled = false;
            }
        }


        private void rtfTerminalIn_TextChanged(object sender, EventArgs e)
        {
            if (rtfTerminalIn.Text.Length > 0)
            {
                btnClearIn.Enabled = true;
            }
        }


        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            RegisterEventHandlers(false);
            SaveSettings();

            try
            {
                _comWrapper.ComportDispose();
            }
            catch (Exception)
            {
                ;//Program should be able to close
            }
        }


        private void btnLogClear_Click(object sender, EventArgs e)
        {
            if(rtfLog.Text.Length > 0)
            {
                rtfLog.Clear();
            }
        }


        private void rtfLog_TextChanged(object sender, EventArgs e)
        {
            rtfLog.SelectionStart = rtfLog.Text.Length;
            rtfLog.Focus();

            if (rtfLog.Text.Length > 0)
            {
                btnLogClear.Enabled = true;
                btnExport.Enabled = true;
            }
            else
            {
                btnLogClear.Enabled = false;
                btnExport.Enabled = false;
            }
        }


        private void log_Enter(object sender, EventArgs e)
        {
            rtfLog.SelectionStart = rtfLog.Text.Length;
            rtfLog.Focus();
        }


        private void logExport_Click(object sender, EventArgs e)
        {
            String logString = "";
            for (int i = 0; i < rtfLog.Lines.Length; i++)
            {
                logString += rtfLog.Lines[i] + Environment.NewLine;
            }
            System.IO.File.WriteAllText(@"log.txt", logString);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////
        #region Main-Tab-InputEvents

        private void mainTabControl_Enter(object sender, EventArgs e)
        {
            btnConnect.Focus();
        }
               
        
        private void btnReadHardware_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_backgroundWorkerHardware.IsBusy)
                {
                    EnableSettingsControls();
                    InitializeBGWTimer();
                    _backgroundWorkerHardware.RunWorkerAsync();
                }
            }
            catch (Exception)
            {
            }
        }


        private void btnReadTemperatur_Click(object sender, EventArgs e)
        {
            ReadTemperature();
        }


        private void ReadTemperature()
        {
            try
            {
                if (!_backgroundWorkerTemperature.IsBusy)
                {
                    _data.Temperature = 0;
                    _data.ADT_Raw = "";
                    if(!chkPollTemp.Checked == true)
                    {
                        EnableSettingsControls();
                    }
                    InitializeBGWTimer();
                    _backgroundWorkerTemperature.RunWorkerAsync();
                }
            }
            catch (Exception)
            {
            }
        }


        private void btnReadADC1_Click(object sender, EventArgs e)
        {
            ReadADC1();
        }


        private void ReadADC1()
        {
            try
            {
                if (!_backgroundWorkerADW.IsBusy)
                {
                    _data.ADW1_Raw = "";
                    if(!chkAD1.Checked == true)
                    {
                        EnableSettingsControls();
                    }
                    InitializeBGWTimer();
                    _backgroundWorkerADW.RunWorkerAsync();
                }
            }
            catch (Exception)
            {
            }
        }


        private void btnReadADC2_Click(object sender, EventArgs e)
        {
            ReadADC2();
        }


        private void ReadADC2()
        {
            try
            {
                if (!_backgroundWorkerADW2.IsBusy)
                {
                    _data.ADW2_Raw = "";
                    if(!chkAD2.Checked == true)
                    {
                        EnableSettingsControls();
                    }
                    InitializeBGWTimer();
                    _backgroundWorkerADW2.RunWorkerAsync();
                }
            }
            catch (Exception)
            {
            }
        }


        private void rbV1_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_backgroundWorkerRange.IsBusy)
                {
                    object command = (char)(Commands.ADW | Commands.RNG1);
                    _range = _data.getRNG1();
                    EnableSettingsControls();
                    InitializeBGWTimer();
                    _backgroundWorkerRange.RunWorkerAsync(command);
                }
            }
            catch (Exception)
            {
            }
        }


        private void rbV2_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_backgroundWorkerRange.IsBusy)
                {
                     object command = (char)(Commands.ADW | Commands.RNG2);

                    _range = _data.getRNG2();
                    EnableSettingsControls();
                    InitializeBGWTimer();
                    _backgroundWorkerRange.RunWorkerAsync(command);
                }
            }
            catch (Exception)
            {
            }
        }


        private void btnReadEeprom_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_backgroundWorkerEepromRead.IsBusy)
                {
                    rtfEprom.Clear();
                    _data.Eprom = "";
                    _stopReceivingEepromData = false;
                    EnableSettingsControls();
                    InitializeBGWTimer();
                    _dispatcher.SetReceivingEmptyData(false);

                    _backgroundWorkerEepromRead.RunWorkerAsync();
                }
            }
            catch (Exception)
            {
            }
        }


        private void btnWriteEprom_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_backgroundWorkerEepromWrite.IsBusy)
                {
                    _data.Eprom = rtfEprom.Text;
                    EnableSettingsControls();
                    InitializeBGWTimer();
                    _backgroundWorkerEepromWrite.RunWorkerAsync();
                }
            }
            catch (Exception)
            {
            }
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////
        #region Synchronisation

        private bool WaitForPendingBytes()
        {
            while (_dispatcher.GetPendingBytes() < 0)
            {
                System.Threading.Thread.Sleep(10);
            }
            return true;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////
        #region TerminalSendData

        private void TerminalSendData()
        {
            String sendData = "";

            if (rtfTerminalOut.Lines.Length > 0)
            {
                sendData = rtfTerminalOut.Lines[rtfTerminalOut.Lines.Length - 1];
            }

            if (chkHex.Checked == true)
            {
                String subString = "";
                char data;
                for (int i = 0; i < sendData.Length; i += 2)
                {
                    try
                    {
                        subString = sendData.Substring(i, 2);
                        int intValue = int.Parse(subString, System.Globalization.NumberStyles.HexNumber);
                        data = (char)intValue;
                        _dispatcher.SendData(Commands.ID_TERMINAL, data);
                    }
                    catch
                    {
                        _error.HexConversionError();
                    }                    
                }
            }
            else
            {
                _dispatcher.SendData(Commands.ID_TERMINAL, sendData);
            } 
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////
        #region Logging

        private void logOutput(char output)
        {
            logOutput(output.ToString());
        }


        private void logOutput(byte[] output)
        {
            String logValue = System.Text.Encoding.Default.GetString(output);
            logOutput(logValue);
        }

        
        private void logOutput(String output)
        {
            Invoke(new MethodInvoker(delegate
            {
                rtfLog.AppendText("OUT:\t" + DateTime.Now.ToString("HH:mm:ss") + "\t" + output + "\n");
            }));
        }


        private void logInput(char input)
        {
            logInput(input.ToString());
        }


        private void logInput(byte[] input)
        {
            logInput(_helper.HexArrayToString(input));
        }


        private void logInput(String input)
        {
            Invoke(new MethodInvoker(delegate
            {
                rtfLog.AppendText("IN:\t" + DateTime.Now.ToString("HH:mm:ss") + "\t" + input + "\n");
            }));
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////
        #region PortStatus

        private void InitializePortTimer()
        {
            portTimer.Interval = 600;
            portTimer.Enabled = true;
            this.portTimer.Tick += new System.EventHandler(this.portTimer_Tick);
        }

        private void portTimer_Tick(object sender, System.EventArgs e)
        {
            bool portAvailable = false;
            string[] availablePortNames = SerialPort.GetPortNames();
            String portName = cmbPortName.Text;

            string waiting = "";

            for (int i = 0; i < availablePortNames.Length; i++)
            {
                if (portName == availablePortNames[i])
                {
                    portAvailable = true;
                }
            }

            for (int i = 0; i < _waitCounter; i++)
            {
                waiting += ".";
            }
            _waitCounter++;
            if (_waitCounter > 4)
            {
                _waitCounter = 1;
            }

            if (_comWrapper.ComportIsOpen())
            {
                PortInUse(portName, waiting);
                return;
            }
            if (portAvailable == true)
            {
                PortAvailable(portName, waiting);
            }
            else
            {
                PortNotAvailable(portName, waiting);
            }
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////
        #region PortStatusControl

        private void PortInUse(string portname, string waiting)
        {
            string text = " is connected";

            _portLabel.Text = portname + text + waiting;
            _portLabel.ForeColor = System.Drawing.Color.Black;

        }


        private void PortAvailable(string portname, string waiting)
        {
            string text = " available";

            _portLabel.Text = portname + text + waiting;
            _portLabel.ForeColor = System.Drawing.Color.Black;
        }


        private void PortNotAvailable(string portname, string waiting)
        {
            string text = " not available! ";
            
            _portLabel.Text = portname + text + waiting;
            _portLabel.ForeColor = System.Drawing.Color.DimGray;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////
        #region Polling

        private void chkPollTemp_CheckedChanged(object sender, EventArgs e)
        {
            if (chkPollTemp.Checked == true)
            {
                tempTimer.Interval = 1000;
                tempTimer.Enabled = true;
                this.tempTimer.Tick += new System.EventHandler(this.tempTimer_Tick);
            }
            else
            {
                tempTimer.Stop();
                tempTimer.Enabled = false;
                this.tempTimer.Tick -= new System.EventHandler(this.tempTimer_Tick);
            }
        }


        private void tempTimer_Tick(object sender, EventArgs e)
        {
            ReadTemperature();
        }


        private void chkAD1_CheckedChanged(object sender, EventArgs e)
        {
            if (chkAD1.Checked == true)
            {
                ad1Timer.Interval = 1000;
                ad1Timer.Enabled = true;
                this.ad1Timer.Tick += new System.EventHandler(this.ad1Timer_Tick);
            }
            else
            {
                ad1Timer.Stop();
                ad1Timer.Enabled = false;
                this.ad1Timer.Tick -= new System.EventHandler(this.ad1Timer_Tick);
            }
        }


        private void ad1Timer_Tick(object sender, EventArgs e)
        {
            ReadADC1();
        }


        private void chkAD2_CheckedChanged(object sender, EventArgs e)
        {
            if (chkAD2.Checked == true)
            {
                ad2Timer.Interval = 1000;
                ad2Timer.Enabled = true;
                this.ad2Timer.Tick += new System.EventHandler(this.ad2Timer_Tick);
            }
            else
            {
                ad2Timer.Stop();
                ad2Timer.Enabled = false;
                this.ad2Timer.Tick -= new System.EventHandler(this.ad2Timer_Tick);
            }
        }


        private void ad2Timer_Tick(object sender, EventArgs e)
        {
            ReadADC2();
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////
        #region Simulation

        private void chkSimulation_CheckedChanged(object sender, EventArgs e)
        {
            if(chkSimulation.Checked == true)
            {
                _simulateResponses = true;
            }
            else
            {
                _simulateResponses = false;
            }
        }


        #endregion

    }
}
