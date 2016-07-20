
//Tool to Convert HydroTerre (HT) Forcing file to RHESSys climate files.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ConvertHTForcingToRHESSys
{
    class Program
    {
        //From https://github.com/RHESSys/RHESSys/wiki/Climate-Inputs
        public class RHESSys_ClimateData
        {
            //precipitation (rain + snow)
            //meters
            public double rain { get; set; }

            //minimum daily temperature
            //°C
            public double tmin { get; set; }

            //maximum daily temperature
            //°C
            public double tmax { get; set; }

            //average daily temperature
            //°C
            public double tavg { get; set; }

            //incoming longwave radiation
            //KJ(meters2/day)

            public double Ldown { get; set; }

            //incoming direct shortwave radiation
            //KJ(meters2/day)
            public double Kdown_direct { get; set; }

            //Vapour pressure deficit
            //Pa
            public double vpd { get; set; }

            //Wind speed
            //meters/sec
            public double wind { get; set; }

            //Relative Humidity
            //Range (0-1)
            public double relative_humidity { get; set; }


        }

        static void Main(string[] args)
        {
            try
            {
                //Note assumptions within code as well
                //Assuming start time is Midnight

                if(args.Count() != 3)
                {
                    Console.WriteLine("Arguments: <xml_input_filename> <output_directory> <project_name> ");
                    Console.WriteLine("");
                    Console.WriteLine("xml_input_filename: Forcing file from HydroTerre");
                    Console.WriteLine("output_directory: Directory where RHESSys ClimateData will be created");
                    Console.WriteLine("project_name: Base filename for RHESSys ClimateData");
                    Console.WriteLine("");
                    return;
                }

                String xml_input_filename = args[0]; // @"W:\ClimateData\HydroTerre_ETV_Data\HT_Forcing.xml";
                String output_directory = args[1]; //@"W:\ClimateData\Test1";
                String project_name = args[2]; //"test";

                DateTime start_time = DateTime.Now;
                Console.WriteLine("Start Time: " + start_time.ToShortTimeString());

                XmlDocument input_xml_filename = new XmlDocument();
                input_xml_filename.Load(xml_input_filename);

                //Assumption: Data Structure is consistent (one huc-12 per forcing file)
                //I.E will not work on HUC-8 scales etc
                int location_Forcing_node = 1;
                int location_Forcing_Inputs = 1;
                int location_Forcing_Outputs = 2;
                int location_Forcing_List = 1;
                int huc_index = 2; //For now one huc12

                //Convert m/day to m/sec
                double meter_per_second = 1.15741e-5;

                XmlNode xml_forcing_node = input_xml_filename.ChildNodes[location_Forcing_node];
                XmlNode xml_forcing_inputs = xml_forcing_node.ChildNodes[location_Forcing_Inputs];
                XmlNode xml_forcing_outputs = xml_forcing_node.ChildNodes[location_Forcing_Outputs];
                XmlNode xml_forcing_list = xml_forcing_outputs.ChildNodes[location_Forcing_List];

                #region Get Start Date
                XmlElement Start_Date_element = xml_forcing_inputs["Start_Date"];
                DateTime start_datetime = Convert.ToDateTime(Start_Date_element.InnerText);
                //CHECK FORMAT NOT CLEAR FROM WIKI
                String rhessys_timestamp = start_datetime.Year + " " + start_datetime.Month + " " + start_datetime.Day + " " + start_datetime.Hour;
                #endregion

                int record_count = xml_forcing_list.ChildNodes.Count;
                List<RHESSys_ClimateData> results = new List<RHESSys_ClimateData>();

                List<double> list_precip = new List<double>();
                List<double> list_temp = new List<double>();
                List<double> list_rh = new List<double>();
                List<double> list_wind = new List<double>();
                List<double> list_rn = new List<double>();
                List<double> list_vp = new List<double>();
                List<double> list_lw = new List<double>();
                List<double> list_Kdown_direct = new List<double>();

                Console.WriteLine("Starting to Load HydroTerre Forcing Data");
                foreach (XmlNode forcing_record in xml_forcing_list.ChildNodes)
                {
                    #region Values for one hour. Averaged over forcing cells at HUC-12
                    XmlNode huc_values = forcing_record.ChildNodes[huc_index];
                    XmlElement DateTime_element = huc_values["DateTime"];
                    DateTime record_date_time = Convert.ToDateTime(DateTime_element.InnerText);

                    XmlElement precip_avg_element = huc_values["Precip_Avg"];
                    XmlElement temp_avg_element = huc_values["Temp_Avg"];
                    XmlElement rh_avg_element = huc_values["RH_Avg"];
                    XmlElement wind_avg_element = huc_values["Wind_Avg"];
                    XmlElement rn_avg_element = huc_values["RN_Avg"];
                    XmlElement vp_avg_element = huc_values["VP_Avg"];
                    XmlElement lw_avg_element = huc_values["LW_Avg"];

                    double precip_avg = Convert.ToDouble(precip_avg_element.InnerText);
                    double temp_avg = Convert.ToDouble(temp_avg_element.InnerText);
                    double rh_avg = Convert.ToDouble(rh_avg_element.InnerText);
                    double wind_avg = meter_per_second * Convert.ToDouble(wind_avg_element.InnerText);
                    double rn_avg = Convert.ToDouble(rn_avg_element.InnerText);
                    double vp_avg = Convert.ToDouble(vp_avg_element.InnerText);
                    double lw_avg = Convert.ToDouble(lw_avg_element.InnerText);

                    list_precip.Add(precip_avg);
                    list_temp.Add(temp_avg);
                    list_rh.Add(rh_avg);
                    list_wind.Add(wind_avg);
                    list_rn.Add(rn_avg);
                    list_vp.Add(vp_avg);
                    list_lw.Add(lw_avg);

                    if (record_date_time.Hour == 23) 
                    {
                        try
                        {
                            //Console.WriteLine(DateTime_element.InnerText);
                            if(list_precip.Count != 24)
                            {
                                Console.WriteLine("WARNING! Record count is not complete for datetime: " + record_date_time.ToLongDateString() + " " + record_date_time.ToLongTimeString());
                                Console.WriteLine("Averaging values for day with only " + list_precip.Count + " records");
                            }

                            RHESSys_ClimateData day_result = new RHESSys_ClimateData();
                            day_result.rain = list_precip.Sum();
                            day_result.tmin = list_temp.Min();
                            day_result.tmax = list_temp.Max();
                            day_result.tavg = list_temp.Average();
                            day_result.relative_humidity = list_rh.Average();
                            day_result.wind = list_wind.Average();
                            day_result.Kdown_direct = list_rn.Average();
                            day_result.vpd = list_vp.Average();
                            day_result.Ldown = list_lw.Sum();

                            results.Add(day_result);

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                            Console.WriteLine("Possible causes include:");
                            Console.WriteLine("\t Corrupted forcing file?");
                            Console.WriteLine("\t List Counts don't match?");
                            Environment.Exit(-1000);
                        }

                        #region Reset lists

                        list_precip.Clear();
                        list_temp.Clear();
                        list_rh.Clear();
                        list_wind.Clear();
                        list_rn.Clear();
                        list_vp.Clear();
                        list_lw.Clear();

                        #endregion
                    }

                    #endregion
                }
                Console.WriteLine("Finished Loading Data");

                #region Simple Checks
                int results_length = results.Count;
                int record_count_days = Convert.ToInt32(record_count / 24);

                if (results_length != record_count_days)
                {
                    Console.WriteLine("WARNING: RECORD COUNTS DO NOT MATCH");
                    Console.WriteLine("results_length = " + results_length);
                    Console.WriteLine("record_count_days = " + record_count_days);
                }

                if(list_precip.Count > 0)
                {
                    Console.WriteLine("WARNING: Data list is not empty. Ignoring " + list_precip.Count + " values to keep records per day");
                }
                #endregion

                #region Write RHESSys_ClimateData Files

                Console.WriteLine("Writing Results");

                var rain_results = from p in results select p.rain;
                String out_fname = output_directory + "\\" + project_name + ".rain";
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(out_fname))
                {
                    file.WriteLine(rhessys_timestamp);
                    foreach (var rain in rain_results)
                        file.WriteLine(rain);
                }

                var tmin_results = from p in results select p.tmin;
                out_fname = output_directory + "\\" + project_name + ".tmin";
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(out_fname))
                {
                    file.WriteLine(rhessys_timestamp);
                    foreach (var tmin in tmin_results)
                        file.WriteLine(tmin);
                }

                var tmax_results = from p in results select p.tmax;
                out_fname = output_directory + "\\" + project_name + ".tmax";
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(out_fname))
                {
                    file.WriteLine(rhessys_timestamp);
                    foreach (var tmax in tmax_results)
                        file.WriteLine(tmax);
                }

                var tavg_results = from p in results select p.tavg;
                out_fname = output_directory + "\\" + project_name + ".tavg";
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(out_fname))
                {
                    file.WriteLine(rhessys_timestamp);
                    foreach (var tmax in tavg_results)
                        file.WriteLine(tmax);
                }

                var relative_humidity_results = from p in results select p.relative_humidity;
                out_fname = output_directory + "\\" + project_name + ".relative_humidity";
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(out_fname))
                {
                    file.WriteLine(rhessys_timestamp);
                    foreach (var relative_humidity in relative_humidity_results)
                        file.WriteLine(relative_humidity);
                }

                var wind_results = from p in results select p.wind;
                out_fname = output_directory + "\\" + project_name + ".wind";
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(out_fname))
                {
                    file.WriteLine(rhessys_timestamp);
                    foreach (var wind in wind_results)
                        file.WriteLine(wind);
                }

                var Kdown_direct_results = from p in results select p.wind;
                out_fname = output_directory + "\\" + project_name + ".Kdown_direct";
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(out_fname))
                {
                    file.WriteLine(rhessys_timestamp);
                    foreach (var Kdown_direct in Kdown_direct_results)
                        file.WriteLine(Kdown_direct);
                }

                var vpd_results = from p in results select p.vpd;
                out_fname = output_directory + "\\" + project_name + ".vpd";
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(out_fname))
                {
                    file.WriteLine(rhessys_timestamp);
                    foreach (var vpd in vpd_results)
                        file.WriteLine(vpd);
                }

                var Ldown_results = from p in results select p.vpd;
                out_fname = output_directory + "\\" + project_name + ".Ldown";
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(out_fname))
                {
                    file.WriteLine(rhessys_timestamp);
                    foreach (var Ldown in Ldown_results)
                        file.WriteLine(Ldown);
                }
                #endregion

                #region Clean up
                results.Clear();
                results = null;
                input_xml_filename = null;

                #endregion

                Console.WriteLine("Finished");
                DateTime end_time = DateTime.Now;
                TimeSpan ts = end_time - start_time;
                Console.WriteLine("End Time: " + end_time.ToShortTimeString());
                Console.WriteLine("Took: " + ts.TotalSeconds);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
