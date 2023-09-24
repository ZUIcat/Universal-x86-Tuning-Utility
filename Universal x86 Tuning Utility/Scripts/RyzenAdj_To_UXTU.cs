﻿using NvAPIWrapper.Display;
using RyzenSmu;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Universal_x86_Tuning_Utility.Scripts.ASUS;
using Universal_x86_Tuning_Utility.Scripts.GPUs.AMD;
using Universal_x86_Tuning_Utility.Scripts.GPUs.NVIDIA;
using Universal_x86_Tuning_Utility.Scripts.Intel_Backend;

namespace Universal_x86_Tuning_Utility.Scripts
{
    internal class RyzenAdj_To_UXTU
    {
        static int i = 0;
        //Translate RyzenAdj like cli arguments to UXTU
        public static async void Translate(string _ryzenAdjString, bool isAutoReapply = false)
        {
            try
            {
                //Remove last space off cli arguments 
                _ryzenAdjString = _ryzenAdjString.Substring(0, _ryzenAdjString.Length - 1);
                //Split cli arguments into array
                string[] ryzenAdjCommands = _ryzenAdjString.Split(' ');
                ryzenAdjCommands = ryzenAdjCommands.Distinct().ToArray();

                //Run through array
                foreach (string ryzenAdjCommand in ryzenAdjCommands)
                {
                    await Task.Run(() =>
                    {
                        try
                        {
                            string command = ryzenAdjCommand;
                            if (!command.Contains("=")) command = ryzenAdjCommand + "=0";
                            // Extract the command string before the "=" sign
                            string ryzenAdjCommandString = command.Split('=')[0].Replace("=", null).Replace("--", null);
                            // Extract the command string after the "=" sign
                            string ryzenAdjCommandValueString = command.Substring(ryzenAdjCommand.IndexOf('=') + 1);
                            
                            if (ryzenAdjCommandString.Contains("ASUS"))
                            {
                                AsusWmi(ryzenAdjCommandString, ryzenAdjCommandValueString);
                                Task.Delay(50);
                            }
                            else if (ryzenAdjCommandString.Contains("Refresh-Rate"))
                            {
                                Universal_x86_Tuning_Utility.Scripts.Misc.Display.ApplySettings(Convert.ToInt32(ryzenAdjCommandValueString));
                            }
                            else if (ryzenAdjCommandString.Contains("ADLX"))
                            {
                                ADLX(ryzenAdjCommandString, ryzenAdjCommandValueString);
                                Task.Delay(50);
                            }
                            else if (ryzenAdjCommandString.Contains("NVIDIA"))
                            {
                                NVIDIA(ryzenAdjCommandString, ryzenAdjCommandValueString);
                                Task.Delay(50);
                            }
                            else if (ryzenAdjCommandString.Contains("intel"))
                            {
                                //Convert value of select cli argument to int
                                int ryzenAdjCommandValue = Convert.ToInt32(ryzenAdjCommandValueString);

                                if (ryzenAdjCommandString == "intel-pl") TDP_Management.changeTDPAll(ryzenAdjCommandValue);
                                else if (ryzenAdjCommandString == "power-limit-1") TDP_Management.changePL1(ryzenAdjCommandValue);
                                else if (ryzenAdjCommandString == "power-limit-2") TDP_Management.changePL2(ryzenAdjCommandValue);
                            }
                            else
                            {
                                //Convert value of select cli argument to uint
                                uint ryzenAdjCommandValue = Convert.ToUInt32(ryzenAdjCommandValueString);
                                if(ryzenAdjCommandValue <= 0 && !ryzenAdjCommandString.Contains("co")) SMUCommands.applySettings(ryzenAdjCommandString, 0x0);
                                else SMUCommands.applySettings(ryzenAdjCommandString, ryzenAdjCommandValue);
                                Task.Delay(50);
                            }
                        }
                        catch (Exception ex) { }
                    });
                }
            }
            catch (Exception ex) { }
        }
        private static void ADLX(string command, string value)
        {
            try
            {
                string[] variables = value.Split('-');

                if (command == "ADLX-Lag") ADLXBackend.SetAntiLag(int.Parse(variables[0]), bool.Parse(variables[1]));
                if (command == "ADLX-Boost") ADLXBackend.SetBoost(int.Parse(variables[0]), bool.Parse(variables[1]), int.Parse(variables[2]));
                if (command == "ADLX-RSR")
                {
                    ADLXBackend.SetRSR(bool.Parse(variables[0]));
                    ADLXBackend.SetRSRSharpness(int.Parse(variables[1]));
                }
                if (command == "ADLX-Chill") ADLXBackend.SetChill(int.Parse(variables[0]), bool.Parse(variables[1]), int.Parse(variables[2]), int.Parse(variables[3]));
                if (command == "ADLX-Sync") ADLXBackend.SetEnhancedSync(int.Parse(variables[0]), bool.Parse(variables[1]));
                if (command == "ADLX-ImageSharp") ADLXBackend.SetImageSharpning(int.Parse(variables[0]), bool.Parse(variables[1]), int.Parse(variables[2]));
            }
            catch { }
        }

        private static void NVIDIA(string command, string value)
        {
            try
            {
                string[] variables = value.Split('-');

                if (command == "NVIDIA-Clocks") NvTuning.SetClocks(int.Parse(variables[0]), int.Parse(variables[1]));
            }
            catch { }
        }

        static bool isMessageBoxOpen = false, isUpdatingUltiMode = false;
        private static void AsusWmi(string command, string value)
        {
            try
            {
                uint id = 0;
                int mode = 0;
                if (command == "ASUS-Power")
                {
                    if (App.product.Contains("ROG") || App.product.Contains("TUF")) id = ASUSWmi.PerformanceMode;
                    else id = ASUSWmi.VivoBookMode;

                    mode = (int)ASUSWmi.AsusMode.Balanced;
                    if(value == "1") mode = (int)ASUSWmi.AsusMode.Silent;
                    else if (value == "2") mode = (int)ASUSWmi.AsusMode.Balanced;
                    else if (value == "3") mode = (int)ASUSWmi.AsusMode.Turbo;
                    if (App.wmi.DeviceGet(id) != mode) App.wmi.DeviceSet(id, mode, "PowerMode");
                }
                if(command == "ASUS-Eco")
                {
                    if(value.ToLower() == "true") App.wmi.SetGPUEco(1);
                    else App.wmi.SetGPUEco(0);
                }
                if (command == "ASUS-MUX")
                {
                    if (!isMessageBoxOpen && !isUpdatingUltiMode)
                    {
                        if (App.product.Contains("ROG") || App.product.Contains("TUF")) id = ASUSWmi.GPUMux;
                        else id = ASUSWmi.GPUMuxVivo;

                        int mux = App.wmi.DeviceGet(id);
                        if (mux > 0 && value.ToLower() == "true")
                        {
                            isMessageBoxOpen = true;

                            var messageBox = new Wpf.Ui.Controls.MessageBox();

                            messageBox.ButtonLeftName = "Restart";
                            messageBox.ButtonRightName = "Cancel";

                            messageBox.ButtonLeftClick += MessageBox_Enable;
                            messageBox.ButtonRightClick += MessageBox_Close;

                            messageBox.Show("GPU Ultimate Mode", "Switching the GPU to Ultimate Mode requires a restart to take\naffect!");


                        }
                        else if (mux < 1 && mux > -1 && value.ToLower() == "false")
                        {
                            isMessageBoxOpen = true;

                            var messageBox = new Wpf.Ui.Controls.MessageBox();

                            messageBox.ButtonLeftName = "Restart";
                            messageBox.ButtonRightName = "Cancel";

                            messageBox.ButtonLeftClick += MessageBox_Disable;
                            messageBox.ButtonRightClick += MessageBox_Close;

                            messageBox.Show("GPU Ultimate Mode", "Disabling GPU Ultimate Mode requires a restart to take\naffect!");
                        }
                    }
                }
            } 
            catch { }
        }

        private static void MessageBox_Enable(object sender, System.Windows.RoutedEventArgs e)
        {
            uint id = 0;
            if (App.product.Contains("ROG") || App.product.Contains("TUF")) id = ASUSWmi.GPUMux;
            else id = ASUSWmi.GPUMuxVivo;
            App.wmi.DeviceSet(id, 0, "MUX");
            Thread.Sleep(250);
            Process.Start("shutdown", "/r /t 1");

            (sender as Wpf.Ui.Controls.MessageBox)?.Close();
            isMessageBoxOpen = false;
            isUpdatingUltiMode = true;
        }

        private static void MessageBox_Disable(object sender, System.Windows.RoutedEventArgs e)
        {
            uint id = 0;
            if (App.product.Contains("ROG") || App.product.Contains("TUF")) id = ASUSWmi.GPUMux;
            else id = ASUSWmi.GPUMuxVivo;
            App.wmi.DeviceSet(id, 1, "MUX");
            Thread.Sleep(250);
            Process.Start("shutdown", "/r /t 1");

            (sender as Wpf.Ui.Controls.MessageBox)?.Close();
            isMessageBoxOpen = false;
            isUpdatingUltiMode = true;
        }

        private static void MessageBox_Close(object sender, System.Windows.RoutedEventArgs e)
        {
            (sender as Wpf.Ui.Controls.MessageBox)?.Close();
            isMessageBoxOpen = false;
        }
    }
}
