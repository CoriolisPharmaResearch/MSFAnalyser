/* 
Copyright (c) 2019, Klaus Richter
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
1. Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.
3. All advertising materials mentioning features or use of this software
   must display the following acknowledgement:
   This product includes software developed by Klaus Richter.
4. Neither the name of the author nor the
   names of its contributors may be used to endorse or promote products
   derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY KLAUS RICHTER ''AS IS'' AND ANY
EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Globalization;

namespace KR
{
    public partial class MSFAnalyser : Form
    {
        string MSFFile;
        int MAX_DATA_POINTS = 100000;
        int MAX_PEAKS = 10000;
        char SeparationChar;
        int ColumnsInFile;
        string[] ColumnsCollection;
        bool loggingOn = true;
        bool independentProgramMode = false;

        string ErgebnisString;
        double HistogramMaxBinStart;
        double HistogramMaxBinStop;
        double HistogramLowestValue;
        double HistogramLargestValue;
        int HistogramSteps;
        double HistogramIntervallSize;
        int CountedHitsInMaxBin;

        struct DataPoint
        {
            public string xTimeString;
            public string yTempString;
            public string yRatioString;
            public double xTime;
            public double yTemp;
            public double yRatio;
        };

        struct TempRampPeak
        {
            public double TempPeakOnsetTime;
            public double MidBaselineTimeBefore;
            public double MidBaselineTimeAfter;
            public double MidBaselineTempBefore;
            public double MidBaselineTempAfter;
            public int BaselineLengthBefore;
            public int BaselineLengthAfter;
            public int PeakLength;
            public double MaxTempPeak;
            public double MaxTempPeakTime;
            public double TempPeakOffsetTime;
            public int PointNumberAtPeakMax;
            public double STDPeakMax;
            public double STDBasePrior;
            public double STDBaseAfter;
            public double SlopeUpRamp;
            public double SlopeDownRamp;
            public double IntervallToPeakBefore;

            public double RatioBasePrior;
            public double RatioPeakOnsetTime;
            public double RatioPeakOffsetTime;
            public double MidBaselineRatioBefore;
            public double AverageBaselineRatioBefore;
            public double STDBaselineRatioBefore;
            public double MidBaselineRatioAfter;
            public double RatioAtTempMaxPeak;
            public double STDOfRatioAtTempBasePrior;
            public double AverageOfRatioAtTempMax;
            public double STDOfRatioAtTempMax;
            public int PointNumberAtRatioPeakMax;
        };

        string settingsFile = "MSFSettings.txt";


        private string GetFromSettingsIfPossible(string searchtag)
        {
            string searchFile = "";
            if (!File.Exists(settingsFile))
            {
                MessageBox.Show("No settings found yet!!\n\n Created New Settings-File");
                StreamWriter settingsWriter = new StreamWriter(settingsFile);
                settingsWriter.Close();
            }
            StreamReader settingsReader = new StreamReader(settingsFile);
            string nextline = "";
            bool locationFound = false;
            while (settingsReader.Peek() > 0)
            {
                nextline = settingsReader.ReadLine();
                string[] nextlinesplit = nextline.Split('\t');
                if (nextlinesplit.Length == 2)
                {
                    if (searchtag == nextlinesplit[0])
                    {
                        locationFound = true;
                        searchFile=nextlinesplit[1];
                    }
                }
            }
            settingsReader.Close();
            return searchFile;
        }

        private void WriteToSettingsInstead(string _searchtag, string location)
        {
            StreamWriter astreamwriter = new StreamWriter(Path.GetFileNameWithoutExtension(settingsFile) + "2.txt");
            string oldlocation = "";
            bool locationFound = false;
            int SettingsReplaced = 0;
            StreamReader astreamreader = new StreamReader(settingsFile);
            while (astreamreader.Peek() > 0)
            {
                string nextline = astreamreader.ReadLine();
                string[] nextlinesplit = nextline.Split('\t');
                if (nextlinesplit.Length == 2)
                {
                    if (nextlinesplit[1] != "")
                    {
                        if (nextlinesplit[0] != _searchtag) astreamwriter.WriteLine(nextline);
                        if (nextlinesplit[0] == _searchtag)
                        {
                            oldlocation = nextlinesplit[1];
                            locationFound = true;
                            astreamwriter.WriteLine(_searchtag + "\t" + location);
                            SettingsReplaced = 1;
                        }
                    }
                    else MessageBox.Show("Error in Settings File in line \n\n"
                        + nextline +
                        "....corrected");
                }
                else MessageBox.Show("Error in Settings File in line \n\n"
                    + nextline +
                    "....corrected");
            }
            astreamwriter.Close();
            astreamreader.Close();
            File.Delete(settingsFile);
            File.Move(Path.GetFileNameWithoutExtension(settingsFile) + "2.txt", settingsFile);
            if (SettingsReplaced == 0)
            {
                StreamWriter astreamwriter2 = new StreamWriter(settingsFile, true);
                astreamwriter2.WriteLine(_searchtag + "\t" + location);
                astreamwriter2.Close();
            }
        }

        private void MakeDataHistogramm(double[] ValueDouble, int ValueNumber, int intervall, string visualizeMethod)
        {
            HistogramSteps = intervall;
            List<int> ResultList = new List<int>();
            double Valuemax = 0;
            double Valuemin = 0;
            for (int i = 0; i < ValueDouble.Length; ++i)
            {
                if (i == 0)
                {
                    Valuemax = ValueDouble[i];
                    Valuemin = ValueDouble[i];
                }
                if (ValueDouble[i] > Valuemax) Valuemax = ValueDouble[i];
                if (ValueDouble[i] < Valuemin) Valuemin = ValueDouble[i];
            }
            HistogramLowestValue = Valuemin;
            HistogramLargestValue = Valuemax;
            for (int i = 0; i < intervall; ++i)
            {
                ResultList.Add(0);
            }
            double intervallstep = (Valuemax - Valuemin) / intervall;
            HistogramIntervallSize = intervallstep;
            for (int i = 0; i < ValueDouble.Length; ++i)
            {
                for (int k = 0; k < intervall; ++k)
                {
                    if ((ValueDouble[i] >= Valuemin + intervallstep * k) && (ValueDouble[i] < Valuemin + intervallstep * (k + 1)))
                    {
                        ResultList[k] = ResultList[k] + 1;
                    }
                }
                if (ValueDouble[i] == Valuemax) ResultList[intervall - 1] = ResultList[intervall - 1] + 1;
            }
            int HitsInMaxBin = 0;
            for (int k = 0; k < intervall; ++k)
            {
                if (k == 0)
                {
                    HitsInMaxBin = ResultList[k];
                    HistogramMaxBinStart = Valuemin + intervallstep * k;
                    HistogramMaxBinStop = Valuemin + intervallstep * (k + 1);
                }
                if (ResultList[k] > HitsInMaxBin)
                {
                    HitsInMaxBin = ResultList[k];
                    HistogramMaxBinStart = Valuemin + intervallstep * k;
                    HistogramMaxBinStop = Valuemin + intervallstep * (k + 1);
                }
            }
            CountedHitsInMaxBin = HitsInMaxBin;
            if (visualizeMethod == "MessageBox")
            {
                ErgebnisString = "";
                for (int k = 0; k < intervall; ++k)
                {
                    ErgebnisString = ErgebnisString + k.ToString() + "\t" + (Valuemin + k * intervallstep).ToString() + "-" + (Valuemin + (k + 1) * intervallstep).ToString() + "\t" + ResultList[k].ToString() + "\n";
                }
                MessageBox.Show(ErgebnisString);
            }
            else if (visualizeMethod == "nothing")
            {
                ErgebnisString = "";
                for (int k = 0; k < intervall; ++k)
                {
                    ErgebnisString = ErgebnisString + k.ToString() + "\t" + (Valuemin + k * intervallstep).ToString() + "-" + (Valuemin + (k + 1) * intervallstep).ToString() + "\t" + ResultList[k].ToString() + "\n";
                }
                //                MessageBox.Show(ErgebnisString);
            }
        }

        private void MakeRangeHistogramm(double[] ValueDouble, int ValueNumber, int intervall, string visualizeMethod, double startValue, double stopValue)
        {
            List<int> ResultList = new List<int>();
            double Valuemax = stopValue;
            double Valuemin = startValue;
            for (int i = 0; i < intervall; ++i)
            {
                ResultList.Add(0);
            }
            double intervallstep = (Valuemax - Valuemin) / intervall;
            for (int i = 0; i < ValueDouble.Length; ++i)
            {
                for (int k = 0; k < intervall; ++k)
                {
                    if ((ValueDouble[i] >= Valuemin + intervallstep * k) && (ValueDouble[i] < Valuemin + intervallstep * (k + 1)))
                    {
                        ResultList[k] = ResultList[k] + 1;
                    }
                }
                if (ValueDouble[i] == Valuemax) ResultList[intervall - 1] = ResultList[intervall - 1] + 1;
            }
            int HitsInMaxBin = 0;
            for (int k = 0; k < intervall; ++k)
            {
                if (k == 0) HitsInMaxBin = ResultList[k];
                if (ResultList[k] > HitsInMaxBin)
                {
                    HitsInMaxBin = ResultList[k];
                    HistogramMaxBinStart = Valuemin + intervallstep * k;
                    HistogramMaxBinStop = Valuemin + intervallstep * (k + 1);
                }
            }
            if (visualizeMethod == "MessageBox")
            {
                ErgebnisString = "";
                for (int k = 0; k < intervall; ++k)
                {
                    ErgebnisString = ErgebnisString + k.ToString() + "\t" + (Valuemin + k * intervallstep).ToString() + "-" + (Valuemin + (k + 1) * intervallstep).ToString() + "\t" + ResultList[k].ToString() + "\n";
                }
                MessageBox.Show(ErgebnisString);
            }
            else if (visualizeMethod == "nothing")
            {
                ErgebnisString = "";
                for (int k = 0; k < intervall; ++k)
                {
                    ErgebnisString = ErgebnisString + k.ToString() + "\t" + (Valuemin + k * intervallstep).ToString() + "-" + (Valuemin + (k + 1) * intervallstep).ToString() + "\t" + ResultList[k].ToString() + "\n";
                }
            }
        }

        private double GetAverage(double[] data)
        {
            double sum = 0;
            for (int x = 0; x < data.Length; ++x)
            {
                sum = sum + data[x];
            }
            return (sum / data.Length);
        }

        private double GetRMSD(double[] Data)
        {
            double chi2 = 0;
            double rmsd = 0;
            double average = GetAverage(Data);
            for (int x = 0; x < Data.Length; ++x)
            {
                chi2 = chi2 + (Data[x] - average) * (Data[x] - average);
            }
            rmsd = Math.Sqrt(chi2 / Data.Length);
            return rmsd;
        }




        public MSFAnalyser()
        {
            string programMode = "IndyProgON";
            string loggingMode = "AllLoggingOFF";
            string versionMode = "VersionInfoON";
            if (programMode == "IndyProgON") independentProgramMode = true;
            else MessageBox.Show("Wrong ProgramMode Setting");
            if (loggingMode == "AllLoggingOFF") loggingOn = false;
            else MessageBox.Show("Wrong LoggingMode Setting");
            InitializeComponent();
            if (independentProgramMode == true)
            {
                MSFFile = GetFromSettingsIfPossible("MSFMultiRampFile");
            }
            else
            {
                MSFFile = GetFromSettingsIfPossible("MSFMultiRampFile");
            }
            label1.Text = "Please select the MSF Output Table";
            if ((File.Exists(MSFFile)) && (MSFFile != "")) label1.Text = MSFFile;
            label1.ForeColor = Color.Red;
            SeparationChar = '\t';
            if ((MSFFile != "") && (File.Exists(MSFFile)))
            {
                CompareSeparators();
            }
            textBox1.Text = DateTime.Now.ToString("yyyyMMdd_hhmmss") + "_MSFMultiOutput_";
        }

        private void CompareSeparators()
        {
            char[] TestSeparator = new char[4] { ',', ';', '\t', ' ' };
            int[] TestColumnsHeader = new int[4] { 0, 0, 0, 0 };
            bool[] TestColumnsMultipleThree = new bool[4] { false, false, false, false };
            int[] TestColumnsFirst = new int[4] { 0, 0, 0, 0 };
            bool[] TestFirstMultipleThree = new bool[4] { false, false, false, false };
            bool[] TestColumnsFirstDouble = new bool[4] { false, false, false, false };
            bool[] TestColumnsSecondDouble = new bool[4] { false, false, false, false };
            bool[] TestColumnsThirdDouble = new bool[4] { false, false, false, false };
            StreamReader aReader = new StreamReader(MSFFile);
            string headerline = aReader.ReadLine();
            for (int x = 0; x < TestSeparator.Length; ++x)
            {
                string[] headerlineSplit = headerline.Split(TestSeparator[x]);
                TestColumnsHeader[x] = headerlineSplit.Length;
                if (TestColumnsHeader[x] % 3 == 0) TestColumnsMultipleThree[x] = true;
            }
            string firstline = aReader.ReadLine();
            firstline = aReader.ReadLine();
            aReader.Close();
            for (int x = 0; x < TestSeparator.Length; ++x)
            {
                string[] firstlineSplit = firstline.Split(TestSeparator[x]);
                TestColumnsFirst[x] = firstlineSplit.Length;
                if (TestColumnsFirst[x] % 3 == 0) TestFirstMultipleThree[x] = true;
                try
                {
                    double test = Convert.ToDouble(firstlineSplit[0], CultureInfo.InvariantCulture);
                    TestColumnsFirstDouble[x] = true;
                }
                catch
                {
                    TestColumnsFirstDouble[x] = false;
                    //                  MessageBox.Show(firstlineSplit[0]);
                }
                try
                {
                    double test = Convert.ToDouble(firstlineSplit[1], CultureInfo.InvariantCulture);
                    TestColumnsSecondDouble[x] = true;
                }
                catch
                {
                    TestColumnsSecondDouble[x] = false;
                    //                    MessageBox.Show(firstlineSplit[1]);
                }
                try
                {
                    double test = Convert.ToDouble(firstlineSplit[2], CultureInfo.InvariantCulture);
                    TestColumnsThirdDouble[x] = true;
                }
                catch
                {
                    TestColumnsThirdDouble[x] = false;
                    //                    MessageBox.Show(firstlineSplit[2]);
                }

            }
            int SeparatorIdentified = 0;
            for (int x = 0; x < TestSeparator.Length; ++x)
            {
                if (TestColumnsMultipleThree[x] == true)
                {
                    if (TestFirstMultipleThree[x] == true)
                    {
                        if (TestColumnsHeader[x] == TestColumnsFirst[x])
                        {
                            if (TestColumnsFirstDouble[x] == true)
                            {
                                if (TestColumnsSecondDouble[x] == true)
                                {
                                    if (TestColumnsThirdDouble[x] == true)
                                    {
                                        SeparationChar = TestSeparator[x];
                                        if (x == 0) comboBox2.Text = ", (Komma)";
                                        else if (x == 1) comboBox2.Text = "; (Semicolon)";
                                        else if (x == 2) comboBox2.Text = "(Tab)";
                                        else if (x == 3) comboBox2.Text = "(Space)";
                                        SeparatorIdentified = 1;
                                        FillComboBox();
                                    }
                                    //                                    else MessageBox.Show("Separator " + x.ToString() + "\tTestColumnsThirdDouble : \t" + TestColumnsThirdDouble[x].ToString());
                                }
                                //                                else MessageBox.Show("Separator " + x.ToString() + "\tTestColumnsSecondDouble : \t" + TestColumnsSecondDouble[x].ToString());
                            }
                            //                            else MessageBox.Show("Separator " + x.ToString() + "\tTestColumnsFirstDouble : \t" + TestColumnsFirstDouble[x].ToString());
                        }
                        //                        else MessageBox.Show("Separator " + x.ToString() + "\tTestColumnsHeader : \t" + TestColumnsHeader[x] + "\tTestColumnsFirst : \t" + TestColumnsFirst[x]);
                    }
                    //                    else MessageBox.Show("Separator " + x.ToString() + "\tTestColumnsFirst : \t" + TestColumnsFirst[x]);
                }
                //                else MessageBox.Show("Separator " + x.ToString() + "\tTestColumnsHeader : \t" + TestColumnsHeader[x]);
            }
            if (SeparatorIdentified == 0) MessageBox.Show("Could not identify the separator myself.\n\nPlease enter it manually");
        }




        private void FillComboBox()
        {
            //            MessageBox.Show(comboBox2.SelectedIndex.ToString());
            if (comboBox2.SelectedIndex == 2)
            {
                SeparationChar = '\t';
            }
            else if (comboBox2.SelectedIndex == 0)
            {
                SeparationChar = ',';
            }
            else if (comboBox2.SelectedIndex == 1)
            {
                SeparationChar = ';';
            }
            else if (comboBox2.SelectedIndex == 3)
            {
                SeparationChar = ' ';
            }
            else
            {
                MessageBox.Show("This character cannot be used\n\nUsing (Tab) instead!");
                SeparationChar = '\t';
            }
            comboBox1.Items.Clear();
            StreamReader aReader = new StreamReader(MSFFile);
            string headerline = aReader.ReadLine();
            string[] headerlineSplit = headerline.Split(SeparationChar);
            ColumnsInFile = headerlineSplit.Length;
            ColumnsCollection = headerlineSplit;
            for (int x = 0; x < headerlineSplit.Length; ++x)
            {
                if (x % 3 == 0) comboBox1.Items.Add(headerlineSplit[x]);
            }
            comboBox1.Items.Add("All");
            comboBox1.Text = comboBox1.Items[0].ToString();
            aReader.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog oFileDialog = new OpenFileDialog();
            if (oFileDialog.ShowDialog() == DialogResult.OK)
            {
                MSFFile = oFileDialog.FileName;
                label1.Text = MSFFile;
                label1.ForeColor = Color.Black;
                if (independentProgramMode == true)
                {
                    WriteToSettingsInstead("MSFMultiRampFile", MSFFile);
                }
                else
                {
                    WriteToSettingsInstead("MSFMultiRampFile", MSFFile);
                }
                if ((MSFFile != "") && (File.Exists(MSFFile)))
                {
                    comboBox1.Items.Clear();
                    StreamReader aReader = new StreamReader(MSFFile);
                    string headerline = aReader.ReadLine();
                    string[] headerlineSplit = headerline.Split(SeparationChar);
                    ColumnsInFile = headerlineSplit.Length;
                    ColumnsCollection = headerlineSplit;
                    for (int x = 0; x < headerlineSplit.Length; ++x)
                    {
                        if (x % 3 == 0) comboBox1.Items.Add(headerlineSplit[x]);
                    }
                    comboBox1.Items.Add("All");
                    comboBox1.Text = "All";
                    aReader.Close();
                }
                else MessageBox.Show("MSF MultiRampFile not found!");
            }
            CompareSeparators();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            int AveragePointsAtPeak=Convert.ToInt32(textBox4.Text);
            int AveragePointsAtBaseline = Convert.ToInt32(textBox5.Text);
            int datasetsInFile = 1;
            int makeAlldatasets = 0;
            if (comboBox1.Text == "All")
            {
                datasetsInFile = ColumnsInFile / 3;
                if (ColumnsInFile % 3 != 0) MessageBox.Show("Strange Column Number, not multiple of 3 (Time, Temp, Ratio)!");
                MessageBox.Show("Doing all columns: " + datasetsInFile);
                makeAlldatasets = 1;
            }
            for (int xx = 0; xx < datasetsInFile; ++xx)
            {
                if (makeAlldatasets == 1) comboBox1.Text = ColumnsCollection[xx * 3];
                int SelectedSetNumber = 1;
                DataPoint[] dPoint = new DataPoint[MAX_DATA_POINTS];
                TempRampPeak[] trPeak = new TempRampPeak[MAX_PEAKS];
                int PointCounter = 0;
                if ((MSFFile != "") && (File.Exists(MSFFile)))
                {
                    StreamReader aReader = new StreamReader(MSFFile);
                    string headerline = aReader.ReadLine();
                    string[] headerlineSplit = headerline.Split(SeparationChar);
                    for (int x = 0; x < headerlineSplit.Length; ++x)
                    {
                        if (comboBox1.Text == headerlineSplit[x]) SelectedSetNumber = x;
                    }
                    string headerline2 = aReader.ReadLine();
                    while (aReader.Peek() > 0)
                    {
                        string nextline = aReader.ReadLine();
                        string[] nextlineSplit = nextline.Split(SeparationChar);
                        dPoint[PointCounter].xTimeString = nextlineSplit[SelectedSetNumber - 2];
                        dPoint[PointCounter].yTempString = nextlineSplit[SelectedSetNumber - 1];
                        dPoint[PointCounter].yRatioString = nextlineSplit[SelectedSetNumber];
                        PointCounter = PointCounter + 1;
                    }
                    aReader.Close();
                }
                else MessageBox.Show("MSF MultiRampFile not found!");
                //                MessageBox.Show("Read in " + PointCounter + " Points\n\n" + dPoint[0].xTimeString + "\t" + dPoint[0].yTempString + "\t" + dPoint[0].yRatioString + "\n\n" + dPoint[1].xTimeString + "\t" + dPoint[1].yTempString + "\t" + dPoint[1].yRatioString + "\n\n" + dPoint[2].xTimeString + "\t" + dPoint[2].yTempString + "\t" + dPoint[2].yRatioString);
                Array.Resize<DataPoint>(ref dPoint, PointCounter);
                double[] ArrayValues = new double[PointCounter];
                for (int x = 0; x < PointCounter; ++x)
                {
                    if (dPoint[x].yTempString != "")
                    {
                        dPoint[x].yTemp = Convert.ToDouble(dPoint[x].yTempString.Replace(",", "."), CultureInfo.InvariantCulture);
                        ArrayValues[x] = dPoint[x].yTemp;
                    }
                    else
                    {
                        dPoint[x].yTemp = -1.0;
                        ArrayValues[x] = -1.0;
                    }
                    if (dPoint[x].xTimeString != "")
                    {
                        dPoint[x].xTime = Convert.ToDouble(dPoint[x].xTimeString.Replace(",", "."), CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        dPoint[x].xTime = -1.0;
                    }
                    if (dPoint[x].yRatioString != "")
                    {
                        dPoint[x].yRatio = Convert.ToDouble(dPoint[x].yRatioString.Replace(",", "."), CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        dPoint[x].yRatio = -1.0;
                    }
                }

                MakeDataHistogramm(ArrayValues, PointCounter, 200, "nothing");

                int SelectedPointCounter = 0;
                double[] dPointSelection = new double[MAX_DATA_POINTS];
                for (int x = 0; x < PointCounter; ++x)
                {
                    if ((dPoint[x].yTemp > HistogramMaxBinStart) && (dPoint[x].yTemp < HistogramMaxBinStop))
                    {
                        dPointSelection[SelectedPointCounter] = dPoint[x].yTemp;
                        SelectedPointCounter = SelectedPointCounter + 1;
                    }
                }
                Array.Resize<double>(ref dPointSelection, SelectedPointCounter);

                double BaselineValue = GetAverage(dPointSelection);
                double STDBaseline = GetRMSD(dPointSelection);
                MakeRangeHistogramm(ArrayValues, PointCounter, 200, "nothing", HistogramMaxBinStart, HistogramMaxBinStop);
                //            MessageBox.Show("HistogramRange:\t "+histogrammMaker.HistogramMaxBinStart + "\t" + histogrammMaker.HistogramMaxBinStop+"\n\nAverages:\t"+BaselineValue +"+/-"+ STDBaseline);

                double Thresholdfactor = Convert.ToDouble(textBox2.Text.Replace(",", "."), CultureInfo.InvariantCulture);
                double PeakOnsetTemp = BaselineValue + Thresholdfactor * STDBaseline;
                double LowerBaselineTemp = BaselineValue - Thresholdfactor * STDBaseline;
                int MinimalBaselineLength = Convert.ToInt32(textBox3.Text);
                int TempRampPeakCounter = 0;
                int BaselineDetected = 0;
                int BaselineStillOK = 0;
                int PeakDetected = 0;
                for (int x = 0; x < PointCounter; ++x)
                {
                    if ((dPoint[x].yTemp > PeakOnsetTemp) && (BaselineDetected == 1))
                    {
                        TempRampPeakCounter = TempRampPeakCounter + 1;
                        trPeak[TempRampPeakCounter].TempPeakOnsetTime = dPoint[x].xTime;
                        trPeak[TempRampPeakCounter].RatioPeakOnsetTime = dPoint[x].yRatio;
                        trPeak[TempRampPeakCounter].MidBaselineTempBefore = dPoint[x - trPeak[TempRampPeakCounter].BaselineLengthBefore / 2].yTemp;
                        trPeak[TempRampPeakCounter].MidBaselineTimeBefore = dPoint[x - trPeak[TempRampPeakCounter].BaselineLengthBefore / 2].xTime;
                        trPeak[TempRampPeakCounter].MidBaselineRatioBefore = dPoint[x - trPeak[TempRampPeakCounter].BaselineLengthBefore / 2].yRatio;
                        double[] CumBasePoints = new double[2 * AveragePointsAtBaseline + 1];
                        for (int y = 0; y < 2 * AveragePointsAtBaseline + 1; ++y)
                        {
                            CumBasePoints[y] = dPoint[x - AveragePointsAtBaseline + y].yRatio;
                        }

                        trPeak[TempRampPeakCounter].AverageBaselineRatioBefore = GetAverage(CumBasePoints);
                        trPeak[TempRampPeakCounter].STDBaselineRatioBefore = GetRMSD(CumBasePoints);
                        trPeak[TempRampPeakCounter - 1].MidBaselineTempAfter = dPoint[x + trPeak[TempRampPeakCounter - 1].BaselineLengthAfter / 2].yTemp;
                        trPeak[TempRampPeakCounter - 1].MidBaselineTimeAfter = dPoint[x + trPeak[TempRampPeakCounter - 1].BaselineLengthAfter / 2].xTime;
                        trPeak[TempRampPeakCounter - 1].MidBaselineRatioAfter = dPoint[x + trPeak[TempRampPeakCounter - 1].BaselineLengthAfter / 2].yRatio;
                        if ((trPeak[TempRampPeakCounter].RatioPeakOnsetTime == -1) && (checkBox4.Checked == true))
                        {
                            int TestTime = x;
                            while (trPeak[TempRampPeakCounter].RatioPeakOnsetTime == -1)
                            {
                                trPeak[TempRampPeakCounter].RatioPeakOnsetTime = dPoint[TestTime].yRatio;
                                TestTime = TestTime + 1;
                            }
                        }
                        BaselineDetected = 0;
                        BaselineStillOK = 1;
                        PeakDetected = 1;
                    }
                    else if ((dPoint[x].yTemp < PeakOnsetTemp) && (dPoint[x].yTemp > LowerBaselineTemp))
                    {
                        if (PeakDetected == 1)
                        {
                            trPeak[TempRampPeakCounter].TempPeakOffsetTime = dPoint[x].xTime;
                            trPeak[TempRampPeakCounter].RatioPeakOffsetTime = dPoint[x].yRatio;
                            if ((trPeak[TempRampPeakCounter].RatioPeakOffsetTime == -1) && (checkBox4.Checked == true))
                            {
                                int TestTime = x;
                                while (trPeak[TempRampPeakCounter].RatioPeakOffsetTime == -1)
                                {
                                    trPeak[TempRampPeakCounter].RatioPeakOffsetTime = dPoint[TestTime].yRatio;
                                    TestTime = TestTime - 1;
                                }
                            }
                            PeakDetected = 0;
                        }
                        trPeak[TempRampPeakCounter + 1].BaselineLengthBefore = trPeak[TempRampPeakCounter + 1].BaselineLengthBefore + 1;
                        if ((TempRampPeakCounter > 0) && (BaselineStillOK == 1)) trPeak[TempRampPeakCounter].BaselineLengthAfter = trPeak[TempRampPeakCounter - 1].BaselineLengthAfter + 1;
                        if (trPeak[TempRampPeakCounter + 1].BaselineLengthBefore > MinimalBaselineLength) BaselineDetected = 1;
                    }
                    else if ((dPoint[x].yTemp > PeakOnsetTemp) && (BaselineDetected == 0))
                    {
                        if (PeakDetected == 0) BaselineStillOK = 0;
                    }
                    else if (dPoint[x].yTemp > LowerBaselineTemp)
                    {
                        if (PeakDetected == 0) BaselineStillOK = 0;
                    }
                    if (PeakDetected == 1)
                    {
                        if (dPoint[x].yTemp > PeakOnsetTemp)
                        {
                            trPeak[TempRampPeakCounter].PeakLength = trPeak[TempRampPeakCounter].PeakLength + 1;
                        }
                        if (dPoint[x].yTemp > trPeak[TempRampPeakCounter].MaxTempPeak)
                        {
                            trPeak[TempRampPeakCounter].MaxTempPeak = dPoint[x].yTemp;
                            trPeak[TempRampPeakCounter].MaxTempPeakTime = dPoint[x].xTime;
                            trPeak[TempRampPeakCounter].RatioAtTempMaxPeak = dPoint[x].yRatio;
                            double[] CumPeakPoints = new double[2*AveragePointsAtPeak+1];
                            int InvalidPoint=0;
                            for (int y=0;y<2*AveragePointsAtPeak+1;++y)
                            {
                                if (x - AveragePointsAtPeak + y < PointCounter) CumPeakPoints[y] = dPoint[x - AveragePointsAtPeak + y].yRatio;
                                else InvalidPoint = InvalidPoint + 1;
                            }
                            if (InvalidPoint > 0) Array.Resize(ref CumPeakPoints, 2 * AveragePointsAtPeak + 1 - InvalidPoint);

                            trPeak[TempRampPeakCounter].AverageOfRatioAtTempMax = GetAverage(CumPeakPoints);
                            trPeak[TempRampPeakCounter].STDOfRatioAtTempMax = GetRMSD(CumPeakPoints);
                        }
                    }
                }
                if (makeAlldatasets == 0) MessageBox.Show("Baseline Mapped: " + BaselineValue + "+/-" + STDBaseline + "\n\nFound TempPeaks: " + TempRampPeakCounter + "\n\n\nWriting OutputFile: " + Path.GetDirectoryName(MSFFile) + "/" + textBox1.Text + "_" + ColumnsCollection[xx * 3] + ".txt");
                StreamWriter aWriter = new StreamWriter(Path.GetDirectoryName(MSFFile) + "/" + textBox1.Text + "_" + ColumnsCollection[xx * 3] + ".txt");
                aWriter.WriteLine("x.ToString()\t" +
                    "BaselineLengthBeforePeak\t" +
                    "TempOfMidBaselineBeforePeak\t" +
                    "TimeOfMidBaselineBeforePeak\t" +
                    "RatioAtMidBaselineBeforePeak\t" +
                    "AvRatioAtMidBaselineBeforePeak\t" +
                    "STDRatioAtMidBaselineBeforePeak\t" +
                    "TimeOfTempPeakOnset\t" +
                    "RatioOfTempPeakOnset\t" +
                    "PeakLength\t" +
                    "TimeOfMaxTempInPeak\t" +
                    "RatioAtMaxTempInPeak\t" +
                    "AvRatioAtMaxTempInPeak\t" +
                    "STDRatioAtMaxTempInPeak\t" +
                    "MaxTempInPeak\t" +
                    "TimeOfTempPeakOutset\t" +
                    "RatioAtTempPeakOutset");

                for (int x = 0; x < TempRampPeakCounter; ++x)
                {
                    aWriter.WriteLine(x.ToString() + "\t"
                        + trPeak[x].BaselineLengthBefore + "\t"
                        + trPeak[x].MidBaselineTempBefore + "\t"
                        + trPeak[x].MidBaselineTimeBefore + "\t"
                        + trPeak[x].MidBaselineRatioBefore + "\t"
                        + trPeak[x].AverageBaselineRatioBefore + "\t"
                        + trPeak[x].STDBaselineRatioBefore + "\t"
                        + trPeak[x].TempPeakOnsetTime + "\t"
                        + trPeak[x].RatioPeakOnsetTime + "\t"
                        + trPeak[x].PeakLength + "\t"
                        + trPeak[x].MaxTempPeakTime + "\t"
                        + trPeak[x].RatioAtTempMaxPeak + "\t"
                        + trPeak[x].AverageOfRatioAtTempMax + "\t"
                        + trPeak[x].STDOfRatioAtTempMax + "\t"
                        + trPeak[x].MaxTempPeak + "\t"
                        + trPeak[x].TempPeakOffsetTime + "\t"
                        + trPeak[x].RatioPeakOffsetTime);

                }
                aWriter.Close();
            }
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            FillComboBox();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            CompareSeparators();
        }





        private void button4_Click(object sender, EventArgs e)
        {
            string VersionText = "";
            string[] version = new string[10];
            string[] versionDate = new string[10];
            version[0] = "Version 0.0:\tSetup of Program";
            versionDate[0] = "20190617";
            //            version[1] = "Version 0.1:\tCorrected Use of Histograms\n\t\tCorrected Use of AverageMedian\n\t\tUse of SettingsReader: ON";
            //            versionDate[1] = "20190621";
            //            version[2] = "Version 0.2:\tAllows Selection of Other Separator";
            //            versionDate[2] = "20190623";
            //            version[3] = "Version 0.3:\tRemoves Bug in Separator Selection\n\t\tImplemets Automatic Separator Detection\n\t\tIncludes Date in OutputFileName\n\t\tImplements 'All' Feature in Selection";
            //            versionDate[3] = "20190625";
            //            version[4] = "Version 0.4:\tStarts Version Numbering (overwriting old versions)\n\t\tUse of SettingsLogger: OFF\n\t\tUse of ProcessTimeLogger: OFF\n\t\tUse of PerformanceLogger: OFF\n\t\tUse of UsageLogger: OFF\n\t\tUse of RoutineProcessLogger: OFF\n\t\tUse of LicenseLogger: OFF\n\t\tUse of ArchiveManager: OFF";
            //            versionDate[4] = "20190630";
            //            version[5] = "Version 0.5:\tHarmonizes Settings Use via individual Constructor";
            //            versionDate[5] = "20190707";
            version[1] = "Version 0.6:\tAverages Peak Range and Baseline Range";
            versionDate[1] = "20190708";
            version[2] = "Version 1.0:\tRelease Version 1.0";
            versionDate[2] = "20191206";
            for (int x = 0; x < version.Length; ++x)
            {
                if (version[x] != "") VersionText = VersionText + "\n" + version[x];
            }
            MessageBox.Show(VersionText);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            string LicenseText = "Copyright (c) 2019, Klaus Richter\nAll rights reserved.\n\nRedistribution and use in source and binary forms, with or without\nmodification, are permitted provided that the following conditions are met:\n1. Redistributions of source code must retain the above copyright\n   notice, this list of conditions and the following disclaimer.\n2. Redistributions in binary form must reproduce the above copyright\n   notice, this list of conditions and the following disclaimer in the   documentation and/or other materials provided with the distribution.\n3. All advertising materials mentioning features or use of this software\n   must display the following acknowledgement:\n   This product includes software developed by Klaus Richter.\n4. Neither the name of the author nor the\n   names of its contributors may be used to endorse or promote products\n   derived from this software without specific prior written permission.\n\nTHIS SOFTWARE IS PROVIDED BY KLAUS RICHTER ''AS IS'' AND ANY\nEXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED\nWARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE\nDISCLAIMED. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY\nDIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES\n(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;\nLOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND\nON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT\n(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS\nSOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.";
            MessageBox.Show(LicenseText);
        }

    }
}
