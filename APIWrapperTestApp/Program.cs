using System.ComponentModel.Design;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using Wszystko_API;
using Wszystko_API.Categories;
using static System.Net.Mime.MediaTypeNames;
using System.Reflection;
using Wszystko_API.Orders.Components;
using Wszystko_API.Shipping;
using static System.Net.WebRequestMethods;
using Wszystko_API.Product;
using Wszystko_API.File;
using System.Net.Mime;
using Wszystko_API.Offers.General_Offer_Model.Components;
using Wszystko_API.Offers.Common_Components;

WszystkoApi wszystkoApi = new WszystkoApi(null);

var test = await wszystkoApi.GenerateDeviceCode();

bool authenticate = false;

Console.WriteLine(test.verificationUriPrettyComplete);

ProcessStartInfo sInfo = new ProcessStartInfo(test.verificationUriPrettyComplete);
sInfo.UseShellExecute = true;
Process Verification = Process.Start(sInfo);

while (!authenticate)
{
    authenticate = await wszystkoApi.CheckForAccessToken();
    Console.WriteLine(authenticate);
}

////var test0 = await wszystkoApi.GetSessions();
////foreach (var session in test0)
////{
////	await wszystkoApi.DeleteConnection(session.Id);
////}

//var test1 = await wszystkoApi.GetAllOffers();
//System.Diagnostics.Debug.WriteLine(test1.NumberOfOffers);

//string imagePath = $"";
//byte[] binaryData = System.IO.File.ReadAllBytes(imagePath);
//BinaryFileResponse x = await wszystkoApi.AddBinaryFile(binaryData);

//var shippingPolicies = await wszystkoApi.GetAllShippingTariffs();
//var complaintPolicies = await wszystkoApi.GetAllComplaintsPolicies();
//var returnPolicies = await wszystkoApi.GetAllReturnPolicies();

//Item[] descriptionItem = new Item[]
//{
//	new Item
//	{
//		ContentType = ContentTypeType.text.ToString(),
//		Value = "Your Text Value"
//	}
//};

//Description[] descriptions = new Description[]
//{
//	new Description
//	{
//		Items = descriptionItem
//	}
//};

//ParameterKit[] parameters = new ParameterKit[]
//{
//	new ParameterKit
//	{
//		Id = 60,
//		Value = 5.45
//	}
//};

//RequestAddProductOffer product = new RequestAddProductOffer()
//{
//	Title = "tytuł",
//	Price = 10,
//	CategoryId = 96,
//	Gallery = new Uri[] { x.Url },
//	VatRate = Wszystko_API.Product.VatRateType.zero.VatRateToString(),
//	LeadTime = Wszystko_API.Product.LeadTimeType.Natychmiast.LeadTimeToString(),
//	StockQuantityUnit = Wszystko_API.Product.StockQuantityUnitType.sztuk.StockQuantityUnitTypeToString(),
//	OfferStatus = Wszystko_API.Product.OfferStatusType.blocked.ToString(),
//	UserQuantityLimit = 50,
//	StockQuantity = 50,
//	GuaranteeId = null,
//	ComplaintPolicyId = complaintPolicies[0].Id.ToString(),
//	ReturnPolicyId = returnPolicies[0].Id.ToString(),
//	ShippingTariffId = shippingPolicies[0].Id.ToString(),
//	Parameters = parameters,
//	Descriptions = descriptions
//};

//var test3 = await wszystkoApi.CreateOffer(product);

//var test4 = await wszystkoApi.GetOfferData("1006723824");
//System.Diagnostics.Debug.WriteLine(test3);

//var test5 = await wszystkoApi.GetAllOrders();
//foreach (var order in test5.simpleOrderModels)
//{
//	Type type = order.GetType();
//	PropertyInfo[] properties = type.GetProperties();

//	foreach (PropertyInfo property in properties)
//	{
//		object value = property.GetValue(order);
//		System.Diagnostics.Debug.WriteLine(value);
//	}
//}

//var test6 = await wszystkoApi.GetOrderWithId("");
//Type type = test6.GetType();
//PropertyInfo[] properties = type.GetProperties();
//foreach (PropertyInfo property in properties)
//{
//    object value = property.GetValue(test6);
//    System.Diagnostics.Debug.WriteLine(value);
//}

//var test7 = await wszystkoApi.GetWaybillsAddedToOrder("");
//System.Diagnostics.Debug.WriteLine(test7);

//var test8 = await wszystkoApi.GetCategoryTreeAndAllParameters();
//StringBuilder sb = new StringBuilder();
//foreach (Wszystko_API.Categories.CategoryBatchInTree categoryBatch in test8)
//{
//	foreach (Wszystko_API.Categories.CategoryInTree category in categoryBatch.Categories)
//	{
//		sb.AppendLine(category.Name + " " + category.Id + category.);
//	}
//}

//string filePath = $"";
//System.IO.File.WriteAllText(filePath, test8.ToString());
//using (StreamWriter writer = new StreamWriter(filePath))
//{
//	writer.Write(sb.ToString());
//}

//var testx = await wszystkoApi.GetCategoryTreeAndAllParameters(8829);


//StringBuilder sb = new StringBuilder();
//string savePath = $"";
//using (StreamWriter writer = new StreamWriter(savePath))
//{
//	var test9 = await wszystkoApi.GetCategoriesByLevel(0);

//	foreach (Wszystko_API.Categories.Category category in test9)
//	{
//		sb.AppendLine($"{category.Name}\t{category.Id}\t{category.ParentId}\t{category.UrlPart}");
//	}
//	writer.WriteLine(sb.ToString());
//}

//var test10 = await wszystkoApi.GetShippingMethods();
//foreach (ShippingModel model in test10)
//{
//    Console.WriteLine($"{model.Id} {model.Name} {model.Shipping} {model.logoUri} {model.AvailableShippingMethodOptions.AdvancePayment} {model.AvailableShippingMethodOptions.CashOnDelivery} {model.MinShippingDays} {model.MaxShippingDays} {model.EarliestEstimatedShippingDate} {model.LatestEstimatedShippingDate}");
//}