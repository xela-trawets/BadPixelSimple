using System;
using System.Net.Http;
HttpClient hpc = new HttpClient();
var uri = new Uri("http://192.168.184.130/cgi-bin/ start.sh?mode=fastsingle");
var response = await hpc.GetAsync(uri);
string Text = await response.Content.ReadAsStringAsync();
