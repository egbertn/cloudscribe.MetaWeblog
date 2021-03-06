﻿// Licensed under the Apache License, Version 2.0
// Author:                  Joe Audette
// Created:                 2016-02-07
// Last Modified:           2019-09-02
// 

using cloudscribe.MetaWeblog.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using System.Xml;

namespace cloudscribe.MetaWeblog.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public class MetaWeblogController : Controller
    {
        public MetaWeblogController(
            IWebHostEnvironment appEnv,
            IMetaWeblogRequestParser metaWeblogRequestParser,
            IMetaWeblogRequestProcessor metaWeblogProcessor,
            IMetaWeblogResultFormatter metaWeblogResultFormatter,
            IMetaWeblogSecurity metaWeblogSecurity,
            IMetaWeblogRequestValidator metaWebLogRequestValidator,
            ILogger<MetaWeblogController> logger,
            IOptions<ApiOptions> optionsAccessor = null)
        {
            HostingEnvironment = appEnv;
            RequestParser = metaWeblogRequestParser;
            RequestProcessor = metaWeblogProcessor;
            ResultFormatter = metaWeblogResultFormatter;
            Security = metaWeblogSecurity;
            RequestValidator = metaWebLogRequestValidator;
            Log = logger;
            if(optionsAccessor != null)
            {
               ApiOptions = optionsAccessor.Value;
            }
            else
            {
                ApiOptions = new ApiOptions(); // just use the default options
            }
           
        }

        protected IWebHostEnvironment HostingEnvironment { get; }
        protected ApiOptions ApiOptions { get; }
        protected IMetaWeblogSecurity Security { get; }
        protected IMetaWeblogRequestProcessor RequestProcessor { get; }
        protected IMetaWeblogRequestParser RequestParser { get; }
        protected IMetaWeblogResultFormatter ResultFormatter { get; }
        protected IMetaWeblogRequestValidator RequestValidator { get; }
        protected ILogger Log { get; }


        [HttpPost]
        [Route("api/metaweblog")]
        public virtual async Task<IActionResult> Index()
        {
            CancellationToken cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;

            var dumpFileBasePath = Path.Combine( HostingEnvironment.ContentRootPath,
				ApiOptions.AppRootDumpFolderVPath);

            XDocument postedXml = null;
            XDocument resultXml;
            MetaWeblogResult outCome;

            try
            {
                using (HttpContext.Request.Body)
                {
                   
                    postedXml = await XDocument.LoadAsync(HttpContext.Request.Body, LoadOptions.None, default);
                   
                }
            }
            catch (Exception ex)
            {
                Log.LogError("oops {0}", ex);

                if (ApiOptions.DumpRequestXmlToDisk)
                {
                    var requestFileName = Path.Combine( dumpFileBasePath , "request-with-error" + Utils.GetDateTimeStringForFileName(true) + ".txt");
                    using var s = System.IO.File.Create(requestFileName);
                    //postedXml.Save(s, SaveOptions.None);
                    await HttpContext.Request.Body.CopyToAsync(s);
                }

                outCome = new MetaWeblogResult
                {
                    Fault = new FaultStruct
                    {
                        faultCode = "802", // invalid access
                        faultString = "invalid access"
                    }
                };
                resultXml = ResultFormatter.Format(outCome);
                return new XmlResult(resultXml);
            }
            

            var metaWeblogRequest = RequestParser.ParseRequest(postedXml);

            if (ApiOptions.DumpRequestXmlToDisk)
            {
                var requestFileName = Path.Combine(dumpFileBasePath , "request-" 
                    + Utils.GetDateTimeStringForFileName(true)  + "-"
                    + metaWeblogRequest.MethodName.Replace(".","-")
                    + ".xml");
                using var s = System.IO.File.Create(requestFileName);
                //await postedXml.SaveAsync(s, SaveOptions.None, default);
                var xmlWriter = XmlWriter.Create(s, settings: new XmlWriterSettings { Async = true });
                await postedXml.WriteToAsync(xmlWriter, default);
                await xmlWriter.FlushAsync();
            }

            var permissions = await Security.ValiatePermissions(metaWeblogRequest, cancellationToken);

            if(string.IsNullOrEmpty(metaWeblogRequest.BlogId))
            {
                metaWeblogRequest.BlogId = permissions.BlogId;
            }
            
            if((!permissions.CanEditPosts)&&(!permissions.CanEditPages))
            {
                outCome = new MetaWeblogResult();
                resultXml = ResultFormatter.Format(outCome);
                return new XmlResult(resultXml);
            }

            var isValid = await RequestValidator.IsValid(metaWeblogRequest, cancellationToken);
            if (!isValid)
            {
                outCome = new MetaWeblogResult();
                outCome.AddValidatonFault();
                resultXml = ResultFormatter.Format(outCome);
                return new XmlResult(resultXml);
            }
            
            outCome = await RequestProcessor.ProcessRequest(
                metaWeblogRequest, 
                permissions, 
                cancellationToken);

            resultXml = ResultFormatter.Format(outCome);

            if (ApiOptions.DumpResponseXmlToDisk)
            {
                var reseponseFileName = Path.Combine(dumpFileBasePath , "response-" 
                    + Utils.GetDateTimeStringForFileName(true) + "-"
                    + outCome.Method.Replace(".", "-") 
                    + ".xml");

                using var s = System.IO.File.Create(reseponseFileName);
                var xmlWriter = XmlWriter.Create(s, settings: new XmlWriterSettings { Async = true });
                await resultXml.WriteToAsync(xmlWriter, default);
                await xmlWriter.FlushAsync();
            }

            return new XmlResult(resultXml);
            
        }

        
    }
}
