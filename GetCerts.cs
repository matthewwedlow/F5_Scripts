using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json; // add a reference to it via the Solution Explorer > Manage NuGet Packages
using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace GetCertificates
{
    class GetCerts
    {
        static void Main(string[] args)
        {
            string username = "yourUserName";
            string password = "yourPassword";

            //Add your F5 IP address to this list
            List<string> F5List = new List<string> { "1.2.3.4", "1.2.3.5", "1.2.3.6" }; 

            //The primary function that loops through the VS, Profiles, and Certificates
            async void GetExpiredCerts(string F5)
            {
                var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                //Get Virtual Server Info           
                string virtualServerPage = "https://" + F5 + "/mgmt/tm/ltm/virtual?expandSubcollections=true"; //Lists all LTM VSs and expands collection
                string virtualServerStringResult = await GetHTML(virtualServerPage); //... Pass the webpage to the GetHTML method which returns the html as a string
                JObject jsonVirtualServerResult = JObject.Parse(virtualServerStringResult);

                //Get SSL Profile Info
                string SSLProfilePage = "https://" + F5 + "/mgmt/tm/ltm/profile/client-ssl"; //Lists all SSL Profiles
                string SSLProfileStringResult = await GetHTML(SSLProfilePage); //... Pass the webpage to the GetHTML method which returns the html as a string
                JObject jsonSSLProfileResult = JObject.Parse(SSLProfileStringResult);

                //Get Certificate Info
                string certificatesPage = "https://" + F5 + "/mgmt/tm/sys/file/ssl-cert?expandSubcollections=true"; //List SSl Certificates
                string certificatesStringResult = await GetHTML(certificatesPage); //... Pass the webpage to the GetHTML method which returns the html as a string
                JObject jsonCertificatesResult = JObject.Parse(certificatesStringResult);

                //Loop through the Virtual Servers and get the SSL profiles
                foreach (JObject item in jsonVirtualServerResult["items"])
                {
                    string virtualServerName = item["name"].ToString();
                    string virtualServerIPPre = item["destination"].ToString();
                    string virtualServerIP = virtualServerIPPre.Remove(0, 8); //Remove the first 8 characters since it responds with /Common/x.x.x.x

                    //Loop through SSL profiles that are applied to the Virtual Server
                    foreach (var item2 in item["profilesReference"]["items"])
                    {
                        //Check only "context" for only Clientside SSL Profiles. Change to "serverside" if you need it
                        string context = item2["context"].ToString();

                        if (context == "clientside")
                        {
                            string virtualServerSSLProfileName = item2["name"].ToString();

                            //Loop through the List of all SSL Profiles
                            foreach (JObject item3 in jsonSSLProfileResult["items"])
                            {
                                string SSLProfileName = item3["name"].ToString();

                                //Check if SSL Profile Names match, if so loop through the Certificates and get expiration
                                if (SSLProfileName == virtualServerSSLProfileName)
                                {
                                    try //SSLProfile Try
                                    {
                                        //Get name of the certificate used in the Profile
                                        string SSLProfileCertPre = item3["cert"].ToString();
                                        string SSLProfileCert = SSLProfileCertPre.Remove(0, 8); //Remove the first 8 characters

                                        //Loop through the Certificates
                                        foreach (JObject item4 in jsonCertificatesResult["items"])
                                        {
                                            try //certificate Name Try
                                            {
                                                string certificateName = item4["name"].ToString();

                                                //Check if the Cert names match, if so get the expration date
                                                if (SSLProfileCert == certificateName)
                                                {
                                                    //You should have all the details at this point. Store whatever you want in a string and output it somehow
                                                    string certificateExpiration = item4["expirationDate"].ToString();
                                                    var certificateExpirationEpoch = epoch.AddSeconds(Convert.ToInt32(item4["expirationDate"])); //Convert time to human readable format
                                                    string commonName = item4["subject"].ToString();
                                                    string SAN = item4["subjectAlternativeName"].ToString();

                                                    Console.WriteLine("F5 = " + F5);
                                                    Console.WriteLine();
                                                    Console.WriteLine("Virtual Server = " + virtualServerName + " " + virtualServerIP);
                                                    Console.WriteLine();
                                                    Console.WriteLine("Certificate = " + certificateName);
                                                    Console.WriteLine();
                                                    Console.WriteLine("SSL Profile = " + SSLProfileName);
                                                    Console.WriteLine();
                                                    Console.WriteLine("Expiration Date = " + certificateExpirationEpoch);
                                                    Console.WriteLine();
                                                    Console.WriteLine("CN = " + commonName);
                                                    Console.WriteLine();
                                                    Console.WriteLine("SAN = " + SAN);
                                                    Console.WriteLine();
                                                    Console.WriteLine();
                                                }
                                            }

                                            catch
                                            {
                                                Console.WriteLine("Possible NULL value in certificate field");
                                                //Console.WriteLine();
                                                //Console.WriteLine();

                                            }
                                        } // End foreach (JObject item4 in jsonCertificatesResult["items"])

                                    }//End Certificate Name Try

                                    catch //Certificate Name catch
                                    {
                                        Console.WriteLine("Possible NULL value in certificate field");
                                        Console.WriteLine();

                                    }
                                }
                            }
                        } //  End foreach (JObject item3 in jsonSSLProfileResult["items"])
                    } // End  if (context == "clientside")

                } // End foreach (JObject item in jsonResult["items"])

            } // End async void GetExpiredCerts(string F5)

            async Task<string> GetHTML(string webpage)
            {
                //Ignore SSL Certificate
                System.Net.ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/JSON"));
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", username, password))));
                    try
                    {
                        using (HttpResponseMessage response = await client.GetAsync(webpage))
                        using (HttpContent content = response.Content)
                        {
                            // ... Read the string.
                            string result = await content.ReadAsStringAsync();
                            return result;
                        }
                    }
                    catch
                    {
                        Console.WriteLine("Unable to connect to HTML page. Check URL, username and password or permissions " + webpage);
                        return null;
                    }
                }
            } // End async Task GetJsonfromHTML(string webpage)

            foreach (string F5 in F5List)
            {
                GetExpiredCerts(F5);
            }
            Console.ReadLine(); //prevent window from closing. Press Enter to close
        }
    }
}
