using System;
using System.Collections.Generic;
using System.Data;
using System.IO; // Path
using System.Linq;
using System.Web; // HttpContext.Server.HttpPostedFileBase
using System.Web.Configuration;
using System.Web.Mvc; // Url.Content()/Server.Content.ActionResult.继承的Controller.[HttpPost]
using controlbindaction.Infrastructure.CustomResults;
using controlbindaction.Infrastructure.Helpers;
using controlbindaction.Models; // DB_HCTKEntities
using Newtonsoft.Json;
using Newtonsoft.Json.Linq; // JObject.JArray
using PagedList; // DbSet<>.ToPagedList()扩展

namespace controlbindaction.Controllers
{
    public class SHLCController : Controller
    {
        private DB_HCTKEntities1 db = new DB_HCTKEntities1();
        // GET: ZipCode
        public ActionResult Index(int page = 1)
        {
            int currentPage = page < 1 ? 1 : page;

            var query = db.TF_SHLC
                        .OrderBy(x => x.BIL_ID)
                        .ThenBy(x => x.MOB_ID)
                        .ThenBy(x => x.ITM);

            var result = query.ToPagedList(currentPage, 10);
            return View(result);//初次调试时没返回result造成无PagedListPager方法内Model実例error
        }

        private string fileSavedPath = WebConfigurationManager.AppSettings["UploadPath"];

        [HttpPost]
        public ActionResult Upload(HttpPostedFileBase file)
        {
            JObject jo = new JObject();
            string result = string.Empty;

            if (file == null)
            {
                jo.Add("Result", false);
                jo.Add("Msg", "請上傳檔案!");
                result = JsonConvert.SerializeObject(jo);
                return Content(result, "application/json");
            }
            if (file.ContentLength <= 0)
            {
                jo.Add("Result", false);
                jo.Add("Msg", "請上傳正確的檔案.");
                result = JsonConvert.SerializeObject(jo);
                return Content(result, "application/json");
            }

            string fileExtName = Path.GetExtension(file.FileName).ToLower();

            if (!fileExtName.Equals(".xls", StringComparison.OrdinalIgnoreCase)
                &&
                !fileExtName.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                jo.Add("Result", false);
                jo.Add("Msg", "請上傳 .xls 或 .xlsx 格式的檔案");
                result = JsonConvert.SerializeObject(jo);
                return Content(result, "application/json");
            }

            try
            {
                var uploadResult = this.FileUploadHandler(file);

                jo.Add("Result", !string.IsNullOrWhiteSpace(uploadResult));
                jo.Add("Msg", !string.IsNullOrWhiteSpace(uploadResult) ? uploadResult : "");

                result = JsonConvert.SerializeObject(jo);
            }
            catch (Exception ex)
            {
                jo.Add("Result", false);
                jo.Add("Msg", ex.Message);
                result = JsonConvert.SerializeObject(jo);
            }
            return Content(result, "application/json");
        }

        /// <summary>
        /// Files the upload handler.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">file;上傳失敗：沒有檔案！</exception>
        /// <exception cref="System.InvalidOperationException">上傳失敗：檔案沒有內容！</exception>
        private string FileUploadHandler(HttpPostedFileBase file)
        {
            string result;

            if (file == null)
            {
                throw new ArgumentNullException("file", "上傳失敗：沒有檔案！");
            }
            if (file.ContentLength <= 0)
            {
                throw new InvalidOperationException("上傳失敗：檔案沒有內容！");
            }

            try
            {
                string virtualBaseFilePath = Url.Content(fileSavedPath);
                string filePath = HttpContext.Server.MapPath(virtualBaseFilePath);

                if (!Directory.Exists(filePath))
                {
                    Directory.CreateDirectory(filePath);
                }

                string newFileName = string.Concat(
                    DateTime.Now.ToString("yyyyMMddHHmmssfff"),
                    Path.GetExtension(file.FileName).ToLower());

                string fullFilePath = Path.Combine(Server.MapPath(fileSavedPath), newFileName);
                file.SaveAs(fullFilePath);

                result = newFileName;
            }
            catch (Exception ex)
            {
                throw;
            }
            return result;
        }

        [HttpPost]
        public ActionResult Import(string savedFileName)
        {
            var jo = new JObject();
            string result;

            try
            {
                var fileName = string.Concat(Server.MapPath(fileSavedPath), "/", savedFileName);//可以拿掉"/",不影响

                var importZipCodes = new List<TF_SHLC>();

                var helper = new ImportDataHelper();
                var checkResult = helper.CheckImportData(fileName, importZipCodes);

                jo.Add("Result", checkResult.Success);
                jo.Add("Msg", checkResult.Success ? string.Empty : checkResult.ErrorMessage);

                if (checkResult.Success)
                {
                    //儲存匯入的資料
                    helper.SaveImportData(importZipCodes);
                }
                result = JsonConvert.SerializeObject(jo);
            }
            catch (Exception ex)
            {
                throw;
            }
            return Content(result, "application/json");
        }

        [HttpPost]
        public ActionResult HasData()
        {
            JObject jo = new JObject();
            bool result = !db.TF_SHLC.Count().Equals(0);
            jo.Add("Msg", result.ToString());
            return Content(JsonConvert.SerializeObject(jo), "application/json");
        }

        public ActionResult Export()
        {
            var exportSpource = this.GetExportData();
            var dt = JsonConvert.DeserializeObject<DataTable>(exportSpource.ToString());

            var exportFileName = string.Concat(
                "审核阶段_",
                DateTime.Now.ToString("yyyyMMdd"),
                ".xlsx");

            return new ExportExcelResult
            {
                SheetName = "单据种类",
                FileName = exportFileName,
                ExportData = dt
            };
        }

        private JArray GetExportData()
        {
            var query = db.TF_SHLC
                          .OrderBy(x => x.BIL_ID)
                          .ThenBy(x => x.MOB_ID)
                          .ThenBy(x => x.ITM);

            JArray jObjects = new JArray();

            foreach (var item in query)
            {
                var jo = new JObject();
                jo.Add("单据别", item.BIL_ID);
                jo.Add("审核模板", item.MOB_ID);
                jo.Add("项次", item.ITM);
                jo.Add("识别码", item.USR);
                jo.Add("摘要", item.REM);
                jo.Add("代理识别码", item.OTHUSR);
                jo.Add("处理时效(分钟)", item.VALID_TIME);
                jo.Add("严重逾期(分钟)", item.DELAY_TIME);
                jo.Add("审核不同意继续往下审", item.TONEXT);
                jObjects.Add(jo);
                var jos = new JObject();
                jos.Add("单据别", String.Empty);
                jos.Add("审核模板", "");
                jos.Add("项次", null);
                jos.Add("识别码", "");
                jos.Add("摘要", "");
                jos.Add("代理识别码", "");
                jos.Add("处理时效(分钟)", null);
                jos.Add("严重逾期(分钟)", null);
                jos.Add("审核不同意继续往下审", "");
                jObjects.Add(jos);
            }
            return jObjects;
        }
    }
}