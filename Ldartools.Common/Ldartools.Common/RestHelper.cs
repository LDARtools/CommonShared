using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Ldartools.Common
{
    public class RestHelper
    {
        public virtual HttpClient GetClient(string username = null, string password = null)
        {
            var client = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (message, certificate2, arg3, arg4) => true }, true);
            client.Timeout = TimeSpan.FromMinutes(5);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authHeader = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.GetEncoding("iso-8859-1").GetBytes($"{username}:{password}")));
                client.DefaultRequestHeaders.Authorization = authHeader;
            }

            return client;
        }

        public async Task<T> GetDataForUrl<T>(string url)
        {
            using (var client = GetClient())
            {
                return await GetDataForUrl<T>(client, url);
            }
        }

        public async Task<T> GetDataForUrl<T>(HttpClient client, string url)
        {
            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(response.ReasonPhrase);
            }

            return JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync());
        }

        public async Task PostDataToUrl(string url, string serializedData)
        {
            HttpContent content = new StringContent(serializedData);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

            using (var client = GetClient())
            {
                var response = await client.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(response.ReasonPhrase);
                }
            }
        }

        public async Task PutDataToUrl(string url, string serializedData)
        {
            HttpContent content = new StringContent(serializedData);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

            using (var client = GetClient())
            {
                var response = await client.PutAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(response.ReasonPhrase);
                }
            }
        }

        public async Task DeleteFromUrl(string url)
        {
            using (var client = GetClient())
            {
                var response = await client.DeleteAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(response.ReasonPhrase);
                }
            }
        }
    }
}
