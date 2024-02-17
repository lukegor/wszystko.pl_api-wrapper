﻿using Newtonsoft.Json;
using System;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using Wszystko_API.Auth;
using Wszystko_API.Categories;
using Wszystko_API.File;
using Wszystko_API.Global_Components;
using Wszystko_API.Integration;
using Wszystko_API.Offers;
using Wszystko_API.Offers.Common_Components;
using Wszystko_API.Offers.General_Offer_Model;
using Wszystko_API.Offers.General_Offer_Model.Components;
using Wszystko_API.Offers.Serial_Offer_Model;
using Wszystko_API.Offers.Serial_Offer_Model.Components;
using Wszystko_API.Offers.Simple_Offer_Model.JsonConverter;
using Wszystko_API.Orders;
using Wszystko_API.Orders.Components;
using Wszystko_API.Payment;
using Wszystko_API.Payment.Components;
using Wszystko_API.Policies.Complaint;
using Wszystko_API.Policies.Guarantee;
using Wszystko_API.Policies.Return;
using Wszystko_API.Product;
using Wszystko_API.Shipping;

namespace Wszystko_API
{
    public class WszystkoApi
    {
        //Wszystko URLs
        private readonly string WszystkoBaseURL = $"https://wszystko.pl/api";

        private string DeviceCode = string.Empty;

        //access token is valid for 12 h
        private string AccessToken = string.Empty;
        private int TokenExpiresIn = -1;
        //RefreshToken is valid for 3 months
        public string RefreshToken = string.Empty;

        private System.Timers.Timer timer = new System.Timers.Timer();

        public delegate void RefreshTokenDelgate();
        /// <summary>
        /// event occures when token is refreshed
        /// </summary>
        public event RefreshTokenDelgate RefreshTokenEvent;


        public WszystkoApi(string RefreshToken, RefreshTokenDelgate refreshtokenevent)
        {
            RefreshTokenEvent += refreshtokenevent;
            this.RefreshToken = RefreshToken;
        }

        public WszystkoApi(RefreshTokenDelgate refreshtokenevent)
        {
            RefreshTokenEvent += refreshtokenevent;
        }

        #region UtilityMethods

        /// <summary>
        /// Use with types such as: string
        /// </summary>
        /// <param name="query"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        private void AddIfNotNullOrEmpty(ref NameValueCollection query, string key, string value)
		{
			if (!string.IsNullOrEmpty(value))
			{
				query.Add(key, value);
			}
		}

		/// <summary>
		/// Use with types such as: int?, bool?, double?, enums, CustomType?
		/// </summary>
		/// <param name="query"></param>
		/// <param name="key"></param>
		/// <param name="value"></param>
		private void AddIfNotNull(ref NameValueCollection query, string key, object value)
		{
			if (value != null)
			{
				query.Add(key, value.ToString());
			}
		}

		private static string ReplaceFirstOccurrence(string input, string oldValue, string newValue)
		{
			int index = input.IndexOf(oldValue);
			if (index != -1)
			{
				// Replace the first occurrence of oldValue with newValue
				return input.Substring(0, index) + newValue + input.Substring(index + oldValue.Length);
			}
			else
			{
				// If oldValue is not found, return the original text
				return input;
			}
		}

		/// <summary>
		/// Supposed to end url links with query string parameters. Not ended and not tested yet
		/// </summary>
		//public void HandleQueryStringParameters()
		//{
		//	Type? type = GetType();//typeof(Task<PaymentMethod[]>);
		//	MethodInfo? method = type.GetMethod("GetSpecificPaymentMethods");
		//	ParameterInfo[]? parametersInfo = method.GetParameters();

		//	bool isFirstParameter = true;

		//	StringBuilder parameters = new StringBuilder();
		//	foreach (var parameter in parametersInfo)
		//	{
		//		string paramName = parameter.Name;

		//		if (!parameter.ParameterType.IsArray)
		//		{
		//			var paramValue = (string)GetType().GetProperty(paramName).GetValue(this);

		//			if (paramValue != null)
		//			{
		//				parameters.Append(isFirstParameter ? "?" : "&");
		//				parameters.Append($"{paramName}={Uri.EscapeDataString(paramValue)}");
		//				isFirstParameter = false;
		//			}
		//		}
		//		else
		//		{
		//			Array paramArray = (Array)GetType().GetProperty(paramName).GetValue(this);
		//			if (paramArray != null)
		//			{
		//				for (int i = 0; i < paramArray.Length; i++)
		//				{
		//					string elementValue = paramArray.GetValue(i).ToString();
		//					parameters.Append(isFirstParameter ? "?" : "&");
		//					parameters.Append($"{paramName}={Uri.EscapeDataString(elementValue)}");
		//					isFirstParameter = false;
		//				}
		//			}
		//		}
		//	}
		//}

		#endregion

		#region AUTH
		/// <summary>
		/// Class automaticly saves Device code for later use
		/// </summary>
		/// <returns></returns>
		public async Task<BaseAuthModel> GenerateDeviceCode()
        {
            using HttpClient client = new HttpClient();

            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("pl-PL"));

            HttpResponseMessage odp = await client.GetAsync(WszystkoBaseURL + "/integration/register");

            Console.WriteLine(odp.Content.ReadAsStringAsync().Result);

            BaseAuthModel model = JsonConvert.DeserializeObject<BaseAuthModel>(odp.Content.ReadAsStringAsync().Result);

            DeviceCode = model.deviceCode;
            return model;
        }

        /// <summary>
        /// check if user granted access
        /// </summary>
        /// <param name="permissions"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<bool> CheckForAccessToken()
        {
            if (DeviceCode == string.Empty) throw new Exception("Device code is empty please use  Authenticate() before this");

            using HttpClient client = new HttpClient();
            //create normal string for client id and client secret

            //send post request to AllegroTokenURL
            HttpResponseMessage odp = await client.GetAsync(WszystkoBaseURL + "/integration/token?deviceCode=" + this.DeviceCode);

            if (odp.StatusCode == System.Net.HttpStatusCode.BadRequest)
                return false;

            //deserialize content of response
            AccessTokenModel model = JsonConvert.DeserializeObject<AccessTokenModel>(odp.Content.ReadAsStringAsync().Result);

            //if user authorized access then remove device code and set other variables for later
            if (model.AccessToken != null)
            {
                DeviceCode = string.Empty;
                AccessToken = model.AccessToken;
                RefreshToken = model.refreshToken;
                TokenExpiresIn = model.expiresIn;

                Console.WriteLine("Dlugosc tokenu: " + (TokenExpiresIn / 2) * 1000);

                this.timer.Interval = (TokenExpiresIn / 2) * 1000;
                this.timer.Start();

                return true;
            }

            return false;
        }

        //public async Task RefreshAccesToken()
        //{
        //    using HttpClient client = new HttpClient();
        //    string formatedstring = $"{ClientID}:{ClientSecret}";

        //    byte[] bytes = Encoding.UTF8.GetBytes(formatedstring);
        //    //create auth string containg normal in base 64
        //    string AuthString = "Basic " + Convert.ToBase64String(bytes);

        //    //add neccessary headers
        //    client.DefaultRequestHeaders.Clear();
        //    client.DefaultRequestHeaders.Add("Authorization", AuthString);

        //    client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("pl-PL"));
        //    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.allegro.public.v1+json"));

        //    var values = new Dictionary<string, string>
        //    {
        //         { "grant_type", "refresh_token" },
        //         { "refresh_token", RefreshToken }
        //    };
        //    var content = new FormUrlEncodedContent(values);

        //    HttpResponseMessage odp = await client.GetAsync(WszystkoBaseURL);

        //    AccessTokenModel model = JsonConvert.DeserializeObject<AccessTokenModel>(odp.Content.ReadAsStringAsync().Result);
        //    if (!odp.IsSuccessStatusCode) return;

        //    //if user authorized access then remove device code and set other variables for later
        //    AccessToken = model.access_token;
        //    RefreshToken = model.refresh_token;

        //    RefreshTokenEvent?.Invoke();
        //    try
        //    {
        //        this.timer.Interval = (TokenExpiresIn / 2) * 1000;
        //    }
        //    catch (ArgumentException)
        //    {
        //        this.timer.Interval = 21599000;
        //    }

        //    this.timer.Start();
        //}

        /// <summary>
        /// Returns list of active sessions (not-terminated connections of external systems with wszystko.pl account)
        /// </summary>
        /// <returns></returns>
        public async Task<Session[]> GetSessions()
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
			//client.DefaultRequestHeaders.Add("Authorization", "Bearer " + AccessToken);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			HttpResponseMessage odp = await client.GetAsync(WszystkoBaseURL + "/me/integrations");

            //System.Diagnostics.Debug.WriteLine(odp.StatusCode);
            Console.WriteLine(odp.Content.ReadAsStringAsync().Result);

            string responseBody = odp.Content.ReadAsStringAsync().Result;
            Session[] sessions = JsonConvert.DeserializeObject<Session[]>(responseBody);

			return sessions;
        }

        /// <summary>
        /// Deletes session with given id
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public async Task<string> DeleteConnection(string sessionId)
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);

            HttpResponseMessage odp = await client.DeleteAsync(WszystkoBaseURL + $" /me/integrations/{sessionId}");

            return odp.Content.ReadAsStringAsync().Result;
        }

        /// <summary>
        /// Deletes all sessions
        /// </summary>
        /// <returns></returns>
        public async Task DeleteAllSessions()
        {
            var test0 = await GetSessions();
            foreach (var session in test0)
            {
                await DeleteConnection(session.Id);
            }
        }

        #endregion

        #region Shipping

        /// <summary>
        /// Gets all shipping methods
        /// </summary>
        /// <returns></returns>
        public async Task<ShippingModel[]> GetShippingMethods()
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage odp = await client.GetAsync(WszystkoBaseURL + $"/shipping/methods");

            string responseBody = odp.Content.ReadAsStringAsync().Result;
            ShippingModel[] shippingMethods = JsonConvert.DeserializeObject<ShippingModel[]>(responseBody);

            return shippingMethods;
        }

        /// <summary>
        /// Gets all shipping tariffs
        /// </summary>
        /// <returns></returns>
        public async Task<ShippingTariffModel[]> GetAllShippingTariffs()
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage odp = await client.GetAsync(WszystkoBaseURL + $"/me/shipping/tariffs");

            string responseBody = odp.Content.ReadAsStringAsync().Result;
            ShippingTariffModel[] shippingTariffs = JsonConvert.DeserializeObject<ShippingTariffModel[]>(responseBody);

            return shippingTariffs;
        }

        /// <summary>
        /// Gets a shipping tariff with given id
        /// </summary>
        /// <param name="resourceStringId"></param>
        /// <returns></returns>
        public async Task<ShippingTariffModel> GetSpecificShippingTariff(string resourceStringId)
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            UriBuilder builder = new UriBuilder(WszystkoBaseURL + $"/me/shipping/tariffs/{resourceStringId}");

            HttpResponseMessage odp = await client.GetAsync(builder.Uri);

            string responseBody = odp.Content.ReadAsStringAsync().Result;
            ShippingTariffModel shippingTariff = JsonConvert.DeserializeObject<ShippingTariffModel>(responseBody);

            return shippingTariff;
        }

        #endregion

        #region Categories

        /// <summary>
        /// Passing categoryLevel equal to 0 returns main categories. Then you can pass Id of a main category to find its subcategories and so on...
        /// </summary>
        /// <param name="categoryLevel"></param>
        /// <returns></returns>
        public async Task<Category[]> GetCategoriesByLevel(int categoryLevel)
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage odp = await client.GetAsync(WszystkoBaseURL + $"/categories/{categoryLevel}/subcategories");

            string responseBody = odp.Content.ReadAsStringAsync().Result;
			Category[] categories = JsonConvert.DeserializeObject<Category[]>(responseBody);


            return categories;
        }

        /// <summary>
        /// Gets all parameters for the given category
        /// </summary>
        /// <param name="categoryId"></param>
        /// <param name="filterable"></param>
        /// <returns></returns>
        public async Task<CategoryInTree> GetCategoryTreeAndParametersPerCategory(int categoryId, bool? filterable)
        {
			using HttpClient client = new HttpClient();
			client.DefaultRequestHeaders.Clear();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			UriBuilder builder = new UriBuilder(WszystkoBaseURL + $"/categories/{categoryId}/parameters");
			NameValueCollection query = System.Web.HttpUtility.ParseQueryString(string.Empty);

            AddIfNotNull(ref query, "filterable", filterable);

            builder.Query = query.ToString();

			HttpResponseMessage odp = await client.GetAsync(builder.Uri);

			string responseBody = odp.Content.ReadAsStringAsync().Result;
            CategoryInTree category = JsonConvert.DeserializeObject<CategoryInTree>(responseBody);

            return category;
        }

        /// <summary>
        /// Gets entire category tree and optionally parameters
        /// </summary>
        /// <param name="includeParameters"></param>
        /// <param name="pageSize"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public async Task<List<CategoryBatchInTree>> GetCategoryTreeAndAllParameters(bool includeParameters = true, int pageSize = 100, int page = 1)
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			CategoryBatchInTree categoryBatch = new CategoryBatchInTree();
            List<CategoryBatchInTree> categoryList = new List<CategoryBatchInTree>();

            do
            {
                //Console.WriteLine(page);
                UriBuilder builder = new UriBuilder(WszystkoBaseURL + $"/categories");
				NameValueCollection query = System.Web.HttpUtility.ParseQueryString(string.Empty);

				query.Add("includeParameters", includeParameters.ToString());
				query.Add("pageSize", pageSize.ToString());

				query.Add("page", page.ToString());
				builder.Query = query.ToString();

				HttpResponseMessage odp = await client.GetAsync(builder.Uri);

                string responseBody = odp.Content.ReadAsStringAsync().Result;
                //System.Diagnostics.Debug.WriteLine(responseBody);
                categoryBatch = JsonConvert.DeserializeObject<CategoryBatchInTree>(responseBody);
                categoryList.Add(categoryBatch);
                ++page;
            } while (page < 158);

            return categoryList;
        }

		#endregion

		#region Offers

		/// <summary>
		/// Gets all offers. May get full or short data based on isFullData function parameter.
		/// </summary>
		/// <param name="isFullData"></param>
		/// <param name="isMyOffers"></param>
		/// <param name="userId"></param>
		/// <returns></returns>
		public async Task<DownloadOfferArrayModel> GetAllOffers(bool isFullData = false, bool isMyOffers = true, string userId = "")
		{
			using HttpClient client = new HttpClient();

			client.DefaultRequestHeaders.Clear();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);

            if (isFullData)
            {
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.pl.wszystko.v1.full+json"));
            }
            else
            {
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			}


			UriBuilder builder = new UriBuilder(WszystkoBaseURL);

            if (isMyOffers)
            {
                builder.Path += $"/me/offers";
			}
            else
            {
                builder.Path += $"/{userId}/offers";
            }

			HttpResponseMessage odp = await client.GetAsync(builder.Uri);

			string odpcontent = odp.Content.ReadAsStringAsync().Result;

            //adjust json property to fit simpler (!isfulldata) implementation of interface
            if (!isFullData)
            {
                ReplaceFirstOccurrence(odpcontent, "records", "offers");
            }

			DownloadOffersModelConverter converter = new DownloadOffersModelConverter(isFullData);

            var settings = new JsonSerializerSettings()
            {
                Converters = new List<JsonConverter> { converter }
            };

			DownloadOfferArrayModel downloadOfferArray = JsonConvert.DeserializeObject<DownloadOfferArrayModel>(odpcontent, settings);

			return downloadOfferArray;
		}

		/// <summary>
		/// Gets your (seller's) offers. Filterable with parameters. May get full or short data based on isFullData function parameter.
		/// </summary>
		/// <param name="phrase"></param>
		/// <param name="shippingTariffId"></param>
		/// <param name="offerStatusTypes"></param>
		/// <param name="orderBy"></param>
		/// <param name="categoryId"></param>
		/// <param name="quantityFrom"></param>
		/// <param name="quantityTo"></param>
		/// <param name="priceFrom"></param>
		/// <param name="priceTo"></param>
		/// <param name="page"></param>
		/// <param name="pageSize"></param>
		/// <param name="hasUserQuantityLimit"></param>
		/// <param name="isMyOffers"></param>
		/// <param name="userId"></param>
		/// <param name="isFullData"></param>
		/// <returns></returns>
		public async Task<DownloadOfferArrayModel> GetSpecificOffers(string phrase, string shippingTariffId,
															General_Offer_Model.Components.OfferStatusType[] offerStatusTypes, OrderByType? orderBy,
                                                            int? categoryId, int? quantityFrom, int? quantityTo,
                                                            double? priceFrom, double? priceTo, int? page, int? pageSize,
                                                            bool? hasUserQuantityLimit, bool isMyOffers = false, string userId = "",
                                                            bool isFullData = false)
		{
			using HttpClient client = new HttpClient();

			client.DefaultRequestHeaders.Clear();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);

            if (!isFullData)
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }
            else
            {
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.pl.wszystko.v1.full+json"));
			}

			UriBuilder builder = new UriBuilder(WszystkoBaseURL);

			if (isMyOffers)
			{
				builder.Path += $"/me/offers";
			}
			else
			{
				builder.Path += $"/{userId}/offers";
			}

			NameValueCollection query = System.Web.HttpUtility.ParseQueryString(string.Empty);

            AddIfNotNullOrEmpty(ref query, "phrase", phrase);
            AddIfNotNullOrEmpty(ref query, "shippingTariffId", shippingTariffId);

            foreach (var statusType in offerStatusTypes)
            {
                query.Add("status", statusType.ToString());
            }

            query.Add("orderBy", orderBy.ToString());

            AddIfNotNull(ref query, "categoryId", categoryId);
            AddIfNotNull(ref query, "quantityFrom", quantityFrom);
            AddIfNotNull(ref query, "quantityTo", quantityTo);
            AddIfNotNull(ref query, "priceFrom", priceFrom);
            AddIfNotNull(ref query, "priceTo", priceTo);
            AddIfNotNull(ref query, "page", page);
            AddIfNotNull(ref query, "pageSize", pageSize);
            AddIfNotNull(ref query, "hasUserQuantityLimit", hasUserQuantityLimit);

            builder.Query = query.ToString();

			HttpResponseMessage odp = await client.GetAsync(builder.Uri);

			string odpcontent = odp.Content.ReadAsStringAsync().Result;
			//System.Console.WriteLine(odpcontent);

			var converter = new DownloadOffersModelConverter(isFullData);
            var settings = new JsonSerializerSettings()
            {
                Converters = new List<JsonConverter> { converter }
            };

			DownloadOfferArrayModel DownloadOfferList = JsonConvert.DeserializeObject<DownloadOfferArrayModel>(odpcontent, settings);

			return DownloadOfferList;
		}

		// not quite clear required arguments
		// title, price, leadtime, stockquantityunit, offerstatus, userquantitylimit, isdraft
		public async Task<ResponseBodyProductOffer> CreateOffer(string title, int price, int categoryId, bool isDraft, VatRateType vatRate, LeadTimeType leadTime,
                                                              StockQuantityUnitType stockQuantityUnitType, General_Offer_Model.Components.OfferStatusType offerStatus, int userQuantityLimit,
                                                              int stockQuantity, Uri[]? photos, string? guaranteeId, string? complaintPolicyId, string? returnPolicyId,
                                                              string? shippingTarrifId, ParameterKit[] parameters, Description[] descriptions, bool showUnitPrice = true)
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            RequestAddProductOffer addProductOffer = new RequestAddProductOffer()
            {
                Title = title,
                Price = price,
                CategoryId = categoryId,
                Gallery = photos,
                VatRate = vatRate.VatRateToString(),
                Parameters = parameters,
			    Descriptions = descriptions,
			    GuaranteeId = guaranteeId,
			    ComplaintPolicyId = complaintPolicyId,
			    ReturnPolicyId = returnPolicyId,
			    ShippingTariffId = shippingTarrifId,
			    LeadTime = leadTime.LeadTimeToString(),
			    StockQuantityUnit = stockQuantityUnitType.StockQuantityUnitTypeToString(),
			    OfferStatus = offerStatus.ToString(),
                UserQuantityLimit = userQuantityLimit,
                IsDraft = isDraft,
                StockQuantity = stockQuantity,
                ShowUnitPrice = showUnitPrice
            };

            string json = JsonConvert.SerializeObject(addProductOffer);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

			HttpResponseMessage odp = await client.PostAsync(WszystkoBaseURL + $"/me/offers", content);
			//System.Diagnostics.Debug.WriteLine(odp.Content.ReadAsStringAsync().Result);

			string responseBody = odp.Content.ReadAsStringAsync().Result;
			ResponseBodyProductOffer responseAddProductOffer = JsonConvert.DeserializeObject<ResponseBodyProductOffer>(responseBody);

            return responseAddProductOffer;
		}

        /// <summary>
        ///
        /// </summary>
        /// <param name="addProductOffer"></param>
        /// <returns></returns>
		public async Task<ResponseBodyProductOffer> CreateOffer(RequestAddProductOffer addProductOffer)
		{
			using HttpClient client = new HttpClient();
			client.DefaultRequestHeaders.Clear();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));


			string json = JsonConvert.SerializeObject(addProductOffer);
			var content = new StringContent(json, Encoding.UTF8, "application/json");

			HttpResponseMessage odp = await client.PostAsync(WszystkoBaseURL + $"/me/offers", content);
			//System.Diagnostics.Debug.WriteLine(odp.Content.ReadAsStringAsync().Result);

			string responseBody = odp.Content.ReadAsStringAsync().Result;
			ResponseBodyProductOffer responseAddProductOffer = JsonConvert.DeserializeObject<ResponseBodyProductOffer>(responseBody);

			return responseAddProductOffer;
		}

        /// <summary>
        /// Gets data for offer with the given id
        /// </summary>
        /// <param name="offerId"></param>
        /// <returns></returns>
		public async Task<DownloadableOfferModel> GetOfferData(string offerId)
        {
			using HttpClient client = new HttpClient();
			client.DefaultRequestHeaders.Clear();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			HttpResponseMessage odp = await client.GetAsync(WszystkoBaseURL + $"/me/offers/{offerId}");
			System.Diagnostics.Debug.WriteLine(odp.Content.ReadAsStringAsync().Result);

            string responseBody = odp.Content.ReadAsStringAsync().Result;
			DownloadableOfferModel offerData = JsonConvert.DeserializeObject<DownloadableOfferModel>(responseBody);

            return offerData;
		}

        /// <summary>
        /// Update an offer with the given id to specific data
        /// </summary>
        /// <param name="offerId"></param>
        /// <param name="offerUpdateContent"></param>
        /// <returns></returns>
        public async Task<string> UpdateOfferData(int offerId, UpdateOfferModel offerUpdateContent)
		{
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);

			var json = JsonConvert.SerializeObject(offerUpdateContent);
			var content = new StringContent(json, Encoding.UTF8, "application/json");

			HttpResponseMessage odp = await client.PutAsync(WszystkoBaseURL + $"/me/offers/{offerId}", content);

            return odp.Content.ReadAsStringAsync().Result;
        }

        /// <summary>
        /// Delete an offer with the given id
        /// </summary>
        /// <param name="offerId"></param>
        /// <returns></returns>
        public async Task<string> DeleteOffer(int offerId)
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			HttpResponseMessage odp = await client.DeleteAsync(WszystkoBaseURL + $"/me/offers/{offerId}");
			System.Diagnostics.Debug.WriteLine(odp.Content.ReadAsStringAsync().Result);

			return odp.Content.ReadAsStringAsync().Result;
        }

        // case-study:
        // in case relevantOfferIds is not provided (empty array), the operation will be applied to all available offers, that are not blocked, instead of being applied to one specific resource
        public async Task<FailedUpdateLogsSet[]> MassUpdateOffers(int[] relevantOfferIds, SerialOfferChangesSet serialOfferModel)
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            SerialOfferChangeModel changeModel = new SerialOfferChangeModel()
            {
                Ids = relevantOfferIds,
                ChangesSet = serialOfferModel
            };

			var json = JsonConvert.SerializeObject(changeModel);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

			UriBuilder builder = new UriBuilder(WszystkoBaseURL + $"/me/update-offers");

            HttpResponseMessage odp = await client.PostAsync(builder.Uri, content);

			string responseBody = await odp.Content.ReadAsStringAsync();
            FailedUpdateLogsSet[] errors = JsonConvert.DeserializeObject<FailedUpdateLogsSet[]>(responseBody);

            return errors;
		}

		#endregion

		#region Files

        /// <summary>
        /// Adds image in binary form
        /// </summary>
        /// <param name="binaryFile"></param>
        /// <returns></returns>
        public async Task<FileResponse> AddBinaryFile(byte[] binaryFile)
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			using MultipartFormDataContent content = new MultipartFormDataContent();
			var fileContent = new ByteArrayContent(binaryFile);
			fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
			{
				Name = $"file--{DateTime.Now.ToString("yyyy-MM-dd--HH-mm-ss")}",
				FileName = $"file-added-at-{DateTime.Now.ToString("yyyy-MM-dd--HH-mm-ss")}"
			};

			content.Add(fileContent);

            HttpResponseMessage odp = await client.PostAsync(WszystkoBaseURL + $"/me/files", content);
			string responseBody = odp.Content.ReadAsStringAsync().Result;

			FileResponse response = JsonConvert.DeserializeObject<FileResponse>(responseBody);

            return response;
        }

        /// <summary>
        /// Adds image[s] from Url
        /// </summary>
        /// <param name="urls"></param>
        /// <returns></returns>
        public async Task<FileResponse[]> AddFileFromUrl(Uri[] urls)
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            string json = JsonConvert.SerializeObject(urls);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage odp = await client.PostAsync(WszystkoBaseURL + $"/me/addFilesFromUrls", content);

            string responsebody = odp.Content.ReadAsStringAsync().Result;
            FileResponse[] responseArray = JsonConvert.DeserializeObject<FileResponse[]>(responsebody);

            return responseArray;
        }

        #endregion

        #region Payment

        /// <summary>
        /// Gets payment methods filtered with function arguments
        /// </summary>
        /// <param name="amount"></param>
        /// <param name="shippingMethods"></param>
        /// <returns></returns>
		public async Task<PaymentMethod[]> GetSpecificPaymentMethods(int? amount, ShippingMethodType[] shippingMethods)
        {
			using HttpClient client = new HttpClient();
			client.DefaultRequestHeaders.Clear();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			UriBuilder builder = new UriBuilder($"{WszystkoBaseURL}/payment/paymentMethods");

			NameValueCollection query = System.Web.HttpUtility.ParseQueryString(string.Empty);

			if (amount.HasValue)
			{
                query.Add("amount", amount.Value.ToString());
			}

			foreach (var shippingMethod in shippingMethods)
			{
                query.Add("shippingMethodTypes", shippingMethod.ToString());
			}

            builder.Query = query.ToString();

			HttpResponseMessage odp = await client.GetAsync(builder.Uri);

            string responseBody = odp.Content.ReadAsStringAsync().Result;
            PaymentMethod[] paymentMethod = JsonConvert.DeserializeObject<PaymentMethod[]>(responseBody);

            return paymentMethod;
        }

        /// <summary>
        /// Gets all payment methods
        /// </summary>
        /// <returns></returns>
		public async Task<PaymentMethod[]> GetAllPaymentMethods()
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage odp = await client.GetAsync(WszystkoBaseURL + $"/payment/paymentMethods");

            string responseBody = odp.Content.ReadAsStringAsync().Result;
            PaymentMethod[] paymentMethods = JsonConvert.DeserializeObject<PaymentMethod[]>(responseBody);

            return paymentMethods;
        }

		#endregion

		#region Guarantees&Complaints&Returns

        /// <summary>
        /// Gets all guarantees
        /// </summary>
        /// <returns></returns>
		public async Task<Guarantee[]> GetAllGuarantees()
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			//client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.pl.wszystko.v1.full+json"));

			HttpResponseMessage odp = await client.GetAsync(WszystkoBaseURL + $"/me/guarantees");

            System.Diagnostics.Debug.WriteLine(odp.Content.ReadAsStringAsync().Result);

            string responseBody = odp.Content.ReadAsStringAsync().Result;
            Guarantee[] guarantees = JsonConvert.DeserializeObject<Guarantee[]>(responseBody);

            return guarantees;
        }

        /// <summary>
        /// Gets a guarantee for the given id
        /// </summary>
        /// <param name="resourceStringId"></param>
        /// <returns></returns>
		public async Task<Guarantee> GetSpecificGuarantees(string resourceStringId)
		{
			using HttpClient client = new HttpClient();
			client.DefaultRequestHeaders.Clear();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			HttpResponseMessage odp = await client.GetAsync(WszystkoBaseURL + $"/me/guarantees/{resourceStringId}");

			string responseBody = odp.Content.ReadAsStringAsync().Result;
			Guarantee guarantee = JsonConvert.DeserializeObject<Guarantee>(responseBody);

			return guarantee;
		}

        /// <summary>
        /// Gets all complaint policies
        /// </summary>
        /// <returns></returns>
		public async Task<Complaint[]> GetAllComplaintsPolicies()
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage odp = await client.GetAsync(WszystkoBaseURL + $"/me/complaintPolicies");

            string responseBody = odp.Content.ReadAsStringAsync().Result;
            Complaint[] complaints = JsonConvert.DeserializeObject<Complaint[]>(responseBody);

            return complaints;
		}

        /// <summary>
        /// Gets a complaint policy for the given id
        /// </summary>
        /// <param name="resourceStringId"></param>
        /// <returns></returns>
		public async Task<Complaint> GetSpecificComplaintsPolicies(string resourceStringId)
		{
			using HttpClient client = new HttpClient();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			HttpResponseMessage odp = await client.GetAsync(WszystkoBaseURL + $"/me/complaintPolicies/{resourceStringId}");

			string responseBody = odp.Content.ReadAsStringAsync().Result;
			Complaint complaints = JsonConvert.DeserializeObject<Complaint>(responseBody);

			return complaints;
		}

        /// <summary>
        /// Gets all return policies
        /// </summary>
        /// <returns></returns>
		public async Task<Return[]> GetAllReturnPolicies()
		{
			using HttpClient client = new HttpClient();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			HttpResponseMessage odp = await client.GetAsync(WszystkoBaseURL + $"/me/returnPolicies");

			string responseBody = odp.Content.ReadAsStringAsync().Result;
			Return[] returns = JsonConvert.DeserializeObject<Return[]>(responseBody);

			return returns;
		}

        /// <summary>
        /// Gets a return policy for the given id
        /// </summary>
        /// <param name="resourceStringId"></param>
        /// <returns></returns>
		public async Task<Return> GetSpecificReturnPolicies(string resourceStringId)
		{
			using HttpClient client = new HttpClient();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			HttpResponseMessage odp = await client.GetAsync(WszystkoBaseURL + $"/me/returnPolicies/{resourceStringId}");

			string responseBody = odp.Content.ReadAsStringAsync().Result;
			Return returns = JsonConvert.DeserializeObject<Return>(responseBody);

			return returns;
		}

		#endregion

		#region Orders

        /// <summary>
        /// Gets all orders
        /// </summary>
        /// <returns></returns>
		public async Task<OrderArrayModel> GetAllOrders()
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			HttpResponseMessage odp = await client.GetAsync(WszystkoBaseURL + $"/me/sales");
            string responseBody = odp.Content.ReadAsStringAsync().Result;

            OrderArrayModel orders = JsonConvert.DeserializeObject<OrderArrayModel>(responseBody);

            return orders;
		}

        /// <summary>
        /// Gets orders filtered with function parameters
        /// </summary>
        /// <param name="phrase"></param>
        /// <param name="creationDateFrom"></param>
        /// <param name="creadtinDateTo"></param>
        /// <param name="statusTypes"></param>
        /// <param name="paymentStatuses"></param>
        /// <param name="shippingMethodIdArray"></param>
        /// <param name="orderBy"></param>
        /// <param name="page"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
		public async Task<OrderArrayModel> GetSpecificOrders(string phrase, DateTime? creationDateFrom, DateTime? creadtinDateTo,
                                                            OrderStatusType[] statusTypes, PaymentStatusType[] paymentStatuses,
                                                            string[] shippingMethodIdArray, OrderByType? orderBy,
                                                            int? page, int? pageSize)
		{
			using HttpClient client = new HttpClient();
			client.DefaultRequestHeaders.Clear();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            UriBuilder builder = new UriBuilder(WszystkoBaseURL + $"/me/sales");

            NameValueCollection query = System.Web.HttpUtility.ParseQueryString(string.Empty);

            AddIfNotNullOrEmpty(ref query, "phrase", phrase);
            AddIfNotNull(ref query, "creationDateFrom", creationDateFrom);
            AddIfNotNull(ref query, "creadtinDateTo", creadtinDateTo);
            AddIfNotNull(ref query, "creadtinDateTo", creadtinDateTo);

            foreach (OrderStatusType statusType in statusTypes)
            {
                query.Add("status", statusType.ToString());
            }
            foreach (var paymentStatus in paymentStatuses)
            {
                query.Add("paymentStatus", paymentStatuses.ToString());
            }
            foreach (var shippingMethodId in shippingMethodIdArray)
            {
                query.Add("shippingMethodId", shippingMethodId);
            }

            AddIfNotNull(ref query, "orderBy", orderBy);
            AddIfNotNull(ref query, "page", page);
            AddIfNotNull(ref query, "pageSize", pageSize);

			HttpResponseMessage odp = await client.GetAsync(builder.Uri);
			string responseBody = odp.Content.ReadAsStringAsync().Result;

			OrderArrayModel orders = JsonConvert.DeserializeObject<OrderArrayModel>(responseBody);

			return orders;
		}

        /// <summary>
        /// Gets an order for the given id
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
		public async Task<SimpleOrderModel> GetOrderWithId(string orderId)
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage odp = await client.GetAsync(WszystkoBaseURL + $" /me/sales/{orderId}");

            string responseBody = odp.Content.ReadAsStringAsync().Result;
            SimpleOrderModel order = JsonConvert.DeserializeObject<SimpleOrderModel>(responseBody);

            return order;
        }

        /// <summary>
        /// Updates order status
        /// </summary>
        /// <param name="updateOrderStatusModel"></param>
        /// <returns></returns>
        public async Task<string> UpdateOrderStatus(UpdateOrderStatusModel updateOrderStatusModel)
        {
			using HttpClient client = new HttpClient();
			client.DefaultRequestHeaders.Clear();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);

			string json = JsonConvert.SerializeObject(updateOrderStatusModel);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage odp = await client.PutAsync(WszystkoBaseURL + $"/me/sales/updateStatus", content);

            return odp.Content.ReadAsStringAsync().Result;
		}

        /// <summary>
        /// Gets waybills for the given order
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task<Waybill[]> GetWaybillsAddedToOrder(string orderId)
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage odp = await client.GetAsync(WszystkoBaseURL + $"/me/sales/{orderId}/trackingNumbers");
			string responseBody = odp.Content.ReadAsStringAsync().Result;

			Waybill[] waybills = JsonConvert.DeserializeObject<Waybill[]>(responseBody);

            return waybills;
        }

        /// <summary>
        /// Updates an order with waybills
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="waybills"></param>
        /// <returns></returns>
        public async Task<string> UpdateOrderWithWaybills(string orderId, Waybill[] waybills)
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);

			string json = JsonConvert.SerializeObject(waybills);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage odp = await client.PutAsync(WszystkoBaseURL + $"/me/sales/{orderId}/trackingNumbers", content);

            return odp.Content.ReadAsStringAsync().Result;
        }

        /// <summary>
        /// Gets statuses of made orders
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task<OrderStatus[]> GetOrdersStatus(string[] orderId)
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            string json = JsonConvert.SerializeObject(orderId);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage odp = await client.PostAsync(WszystkoBaseURL + $"/me/sales/retrieve-statuses", content);

            string responseBody = odp.Content.ReadAsStringAsync().Result;
            OrderStatus[] ordersStatus = JsonConvert.DeserializeObject<OrderStatus[]>(responseBody);

            return ordersStatus;
        }

		#endregion
	}
}