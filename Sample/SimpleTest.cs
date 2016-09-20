﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using J2534DotNet;
using OBD;

namespace Sample
{
    using J2534DotNet.Logger;
    using System.Runtime.InteropServices;
    using System.Threading;

    public partial class SimpleTest : Form
    {
        bool connected = false;
        J2534Extended passThru;
        UDSConnectionFord comm;
        public SimpleTest()
        {
            InitializeComponent();
            passThru = new J2534Extended();
        }

        /*
         *  Example 1:
         *      Detect J2534 devices
         * 
         */
        private void CmdDetectDevicesClick(object sender, EventArgs e)
        {
            // Calling J2534.GetAvailableDevices() will return a list of installed J2534 devices
            List<J2534Device> availableJ2534Devices = J2534Detect.ListDevices();
            if (availableJ2534Devices.Count == 0)
            {
                MessageBox.Show("Could not find any installed J2534 devices.");
                return;
            }


            foreach (J2534Device device in availableJ2534Devices)
            {
                log.Text += device.Name + ", " + device.Vendor + "\r\n\r\n";
                log.Text += "\tConfig Application:\t" + device.ConfigApplication + "\r\n";
                log.Text += "\tFunction Library:\t" + device.FunctionLibrary + "\r\n\r\n";
                log.Text += "\tProtocol\t\tChannels\r\n";
                log.Text += "\tCAN\t\t" + device.CANChannels + "\r\n";
                log.Text += "\tISO15765\t" + device.ISO15765Channels + "\r\n";
                log.Text += "\tISO14230\t" + device.ISO14230Channels + "\r\n";
                log.Text += "\tISO9141\t\t" + device.ISO9141Channels + "\r\n";
                log.Text += "\tJ1850PWM\t" + device.J1850PWMChannels + "\r\n";
                log.Text += "\tJ1850PWM\t" + device.J1850VPWChannels + "\r\n";
                log.Text += "\tSCI_A_ENGINE\t" + device.SCI_A_ENGINEChannels + "\r\n";
                log.Text += "\tSCI_A_TRANS\t" + device.SCI_A_TRANSChannels + "\r\n";
                log.Text += "\tSCI_B_ENGINE\t" + device.SCI_B_ENGINEChannels + "\r\n";
                log.Text += "\tSCI_B_TRANS\t" + device.SCI_B_TRANSChannels + "\r\n\r\n";
            }
        }

        /*
         * 
         *  Example 2:
         *      Use the J2534 protocol to send and receive a message (w/o error checking)
         * 
         */
        private void SendReceiveNoErrorChecking(object sender, EventArgs e)
        {
            J2534Extended passThru = new J2534Extended();

            if (!connected) Connect();

            // Attempt to open a communication link with the pass thru device
            int deviceId = 0;
            passThru.PassThruOpen(IntPtr.Zero, ref deviceId);

            // Open a new channel configured for ISO15765 (CAN)
            int channelId = 0;
            passThru.PassThruConnect(deviceId, ProtocolID.ISO15765, ConnectFlag.NONE, BaudRate.ISO15765_125000, ref channelId);

            // Set up a message filter to watch for response messages
            int filterId = 0;
            PassThruMsg maskMsg = new PassThruMsg(
                ProtocolID.ISO15765,
                TxFlag.ISO15765_FRAME_PAD,
                new byte[] { 0xff, 0xff, 0xff, 0xff });
            PassThruMsg patternMsg = new PassThruMsg(
                ProtocolID.ISO15765,
                TxFlag.ISO15765_FRAME_PAD,
                new byte[] { 0x00, 0x00, 0x07, 0xE8 });
            PassThruMsg flowControlMsg = new PassThruMsg(
                ProtocolID.ISO15765,
                TxFlag.ISO15765_FRAME_PAD,
                new byte[] { 0x00, 0x00, 0x07, 0xE0 });

            IntPtr maskMsgPtr = maskMsg.ToIntPtr();
            IntPtr patternMsgPtr = patternMsg.ToIntPtr();
            IntPtr flowControlMsgPtr = flowControlMsg.ToIntPtr();
            passThru.PassThruStartMsgFilter(channelId, FilterType.FLOW_CONTROL_FILTER, maskMsgPtr, patternMsgPtr, flowControlMsgPtr, ref filterId);

            // Clear out the response buffer so we know we're getting the freshest possible data
            passThru.ClearRxBuffer(channelId);

            // Finally we can send the message!
            PassThruMsg txMsg = new PassThruMsg(
                ProtocolID.ISO15765,
                TxFlag.ISO15765_FRAME_PAD,
                new byte[] { 0x00, 0x00, 0x07, 0xdf, 0x01, 0x00 });
            var txMsgPtr = txMsg.ToIntPtr();
            int numMsgs = 1;
            passThru.PassThruWriteMsgs(channelId, txMsgPtr, ref numMsgs, 50);
            
            // Read messages in a loop until we either timeout or we receive data
            numMsgs = 1;
            IntPtr rxMsgs = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(PassThruMsg)) * numMsgs);
            J2534Err status = J2534Err.STATUS_NOERROR;
            while (J2534Err.STATUS_NOERROR == status)
                status = passThru.PassThruReadMsgs(channelId, rxMsgs, ref numMsgs, 200);

            // If we received data, we want to extract the data of interest.  I'm removing the reflection of the transmitted message.
            if ((J2534Err.ERR_BUFFER_EMPTY == status || J2534Err.ERR_TIMEOUT == status) && numMsgs > 0)
            {
                foreach (PassThruMsg msg in rxMsgs.AsList<PassThruMsg>(numMsgs))
                {
                    //
                    //
                    // Now do something with the data!
                    //
                    //
                }
            }


            // Disconnect this channel
            passThru.PassThruDisconnect(channelId);

            // When we are done with the device, we can free the library.
            passThru.FreeLibrary();
        }

        /*
         * 
         *  Use the J2534 protocol to read voltage
         * 
         */
        private void CmdReadVoltageClick(object sender, EventArgs e)
        {
            J2534Extended passThru = new J2534Extended(); ;// = Loader.Lib;
            double voltage = 0;

            if (!connected) Connect();

            
            if (!comm.DetectProtocol())
            {
                MessageBox.Show(String.Format("Error connecting to device. Error: {0}", comm.GetLastError()));
                //Disconnect();
                return;
            }
            if (!comm.GetBatteryVoltage(ref voltage))
            {
                MessageBox.Show(String.Format("Error reading voltage.  Error: {0}", comm.GetLastError()));
                //Disconnect();
                return;
            }
            //Disconnect();

            // When we are done with the device, we can free the library.
            passThru.FreeLibrary();
            txtVoltage.Text = voltage + @" V";
        }

        private void CmdReadVinClick(object sender, EventArgs e)
        {
            string vin = "";

            try
            {
                J2534Extended passThru = new J2534Extended();
                if (!connected) Connect();
                bool UDSConnection = comm.DetectProtocol();
                if (!UDSConnection) MessageBox.Show("Failed to create OBD connection. Is the ignition on?");
                vin = comm.GetVin();

            }
            catch (OBDException obdEx)
            {
                MessageBox.Show("Error retrieving VIN due to OBD error: " + obdEx.Message);
            }
            catch (J2534Exception j2534Ex)
            {
                MessageBox.Show("Error retrieving VIN due to J2534 error: " + j2534Ex.Message);
            } catch (Exception ex)
            {
                MessageBox.Show("Unknown error occured whilst retrieving VIN: " + ex.Message);
            }

            txtReadVin.Text = vin;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            MessageBox.Show(Assembly.GetExecutingAssembly().Location);
        }


        void Connect()
        {
            if (connected) return;


            if (!LoadJ2534()) return;
            

            comm = new UDSConnectionFord(passThru);


            if (!comm.DetectProtocol())
            {
                MessageBox.Show(String.Format("Error connecting to device. Error: {0}", comm.GetLastError()));
                connected = false;
            }
            else
            {
                connected = true;
            }

            
        }

        bool LoadJ2534()
        {

            if (passThru.IsLoaded) return true;

            J2534Device j2534Device;

            // Find all of the installed J2534 passthru devices
            List<J2534Device> availableJ2534Devices = J2534Detect.ListDevices();
            if (availableJ2534Devices.Count == 0)
            {
                MessageBox.Show("Could not find any installed J2534 devices.");
                return false;
            }

            if (checkBoxLogJ2534.Checked)
            {
                j2534Device = new J2534Device();
                j2534Device.FunctionLibrary = System.IO.Directory.GetCurrentDirectory() + "\\" + "J2534DotNet.Logger.dll";
                Thread.Sleep(10);
                var loaded = passThru.LoadLibrary(j2534Device);
                return loaded;
            }

            //If there is only one DLL to choose from then load it
            if (availableJ2534Devices.Count == 1) passThru.LoadLibrary(availableJ2534Devices[0]);
            else
            {
                var sd = new SelectDevice();
                if (sd.ShowDialog() == DialogResult.OK)
                {
                    j2534Device = sd.Device;
                    var loaded = passThru.LoadLibrary(j2534Device);
                    return loaded;
                }
            }
            return false;
        }

        void Disconnect()
        {
            if (!connected) comm.Disconnect();
            connected = false;
            passThru.FreeLibrary();

        }

        void UpdateLog(string text)
        {
            log.Text += text + Environment.NewLine;
        }

        private void SecurityLevel1_Click(object sender, EventArgs e)
        {
            try
            {
                if (!connected) Connect();

                if (!comm.DetectProtocol())
                {
                    MessageBox.Show("Failed to create OBD connection. Is the ignition on?");
                    return;
                }

                float voltage = comm.PassThruSetProgrammingVoltage(PinNumber.PIN_13, 18000);
                if(voltage < 15000)
                {
                    MessageBox.Show("Failed to set programming voltage");
                    return;
                }
                MessageBox.Show("Please turn the vehicle ignition off, wait 3 seconds, then turn it back on");


                if (!comm.SecurityAccess(0x01))
                {
                    MessageBox.Show(String.Format("Error entering security mode.  Error: {0}", comm.GetLastError()));
                }
                else
                {
                    byte[] memory;
                    comm.ReadMemoryByAddress(0, out memory);
                    //ssageBox.Show("Successfull entered level 1 security mode!");
                }

            }
            catch (OBDException obdEx)
            {
                MessageBox.Show("Error retrieving VIN due to OBD error: " + obdEx.Message);
            }
            catch (J2534Exception j2534Ex)
            {
                MessageBox.Show("Error retrieving VIN due to J2534 error: " + j2534Ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unknown error occured whilst retrieving VIN: " + ex.Message);
            }

            //Download the PCM now
            //byte[] bytes;
            //UpdateLog("ReadMemoryByAddress");

            //bool success = comm.ReadMemoryByAddress(0, out bytes);

            //comm.Disconnect();

            // When we are done with the device, we can free the library.
            //passThru.FreeLibrary();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {

            Disconnect();
        }

        private void checkBoxLogJ2534_CheckStateChanged(object sender, EventArgs e)
        {

        }

        bool toggle = false;
        private void setProgrammingVoltage(object sender, EventArgs e)
        {
            SetVoltage();
        }

        void SetVoltage(bool off = false)
        {
            try
            {
                if (!LoadJ2534())
                {
                    UpdateLog("Failed to load J2534 library");
                    return;
                }

                if (comm == null) comm = new UDSConnectionFord(passThru);

                if (off)
                {
                    UpdateLog("setProgrammingVoltage(PinNumber.PIN_13, OFF)");
                    float programmingVoltage = comm.PassThruSetProgrammingVoltage(PinNumber.PIN_13, 0xFFFFFFFF);
                    UpdateLog("Voltage = : " + programmingVoltage);
                    toggle = false;
                }
                else
                {
                    uint volts = 0;
                    if (!UInt32.TryParse(textBoxVolts.Text, out volts)) return;

                    UpdateLog("setProgrammingVoltage(PinNumber.PIN_13 " + volts);
                    float programmingVoltage = comm.PassThruSetProgrammingVoltage(PinNumber.PIN_13, volts);
                    UpdateLog("Voltage = : " + programmingVoltage);
                    toggle = true;
                }

            }
            catch (J2534Exception j2534Ex)
            {
                UpdateLog("Error retrieving VIN due to J2534 error: " + j2534Ex.Message);
                MessageBox.Show("Error retrieving VIN due to J2534 error: " + j2534Ex.Message);
            }
            catch (Exception ex)
            {
                UpdateLog("Unknown error occured whilst retrieving VIN: " + ex.Message);
                MessageBox.Show("Unknown error occured whilst retrieving VIN: " + ex.Message);
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            SetVoltage(true);
        }
    }
}
