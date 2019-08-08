using System;
using System.Collections.Generic;
using System.IO;// FileInfo 
using System.Linq;
using System.Text;// StringBuilder
using System.Web;
using controlbindaction.Models;// List<TF_SHLC>
using LinqToExcel;// ExcelQueryFactory.AddMapping<T>(委托)

namespace controlbindaction.Infrastructure.Helpers
{
    public class ImportDataHelper
    {
        /// <summary>
        /// 檢查匯入的 Excel 資料.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="importZipCodes">The import zip codes.</param>
        /// <returns></returns>
        public CheckResult CheckImportData(
            string fileName,
            List<TF_SHLC> importZipCodes)
        {
            var result = new CheckResult();// 同一命名空间类，不用全名或using

            var targetFile = new FileInfo(fileName);

            if (!targetFile.Exists)
            {
                result.ID = Guid.NewGuid();
                result.Success = false;
                result.ErrorCount = 0;
                result.ErrorMessage = "匯入的資料檔案不存在";
                return result;
            }

            var excelFile = new ExcelQueryFactory(fileName);

            //欄位對映
            excelFile.AddMapping<TF_SHLC>(x => x.BIL_ID, "单据别");// 
            excelFile.AddMapping<TF_SHLC>(x => x.MOB_ID, "审核模版");
            excelFile.AddMapping<TF_SHLC>(x => x.ITM, "项次");
            excelFile.AddMapping<TF_SHLC>(x => x.USR, "识别码");
            excelFile.AddMapping<TF_SHLC>(x => x.REM, "摘要");
            excelFile.AddMapping<TF_SHLC>(x => x.OTHUSR, "代理识别码");
            excelFile.AddMapping<TF_SHLC>(x => x.VALID_TIME, "处理时效(分钟)");
            excelFile.AddMapping<TF_SHLC>(x => x.DELAY_TIME, "严重逾期(分钟)");
            excelFile.AddMapping<TF_SHLC>(x => x.TONEXT, "审核不同意继续往下审");

            //SheetName
            var excelContent = excelFile.Worksheet<TF_SHLC>("单据审核阶段");

            int errorCount = 0;
            int rowIndex = 1;
            var importErrorMessages = new List<string>();

            //檢查資料
            foreach (var row in excelContent)
            {
                var errorMessage = new StringBuilder();
                var zipCode = new TF_SHLC();// 类的粒度

                zipCode.BIL_ID = row.BIL_ID;
                zipCode.MOB_ID = row.MOB_ID;
                zipCode.ITM = row.ITM;
                //zipCode.CreateDate = DateTime.Now;
                zipCode.USR = row.USR;
                zipCode.REM = row.REM;
                zipCode.OTHUSR = row.OTHUSR;
                zipCode.VALID_TIME = row.VALID_TIME;
                zipCode.DELAY_TIME = row.DELAY_TIME;
                zipCode.TONEXT = row.TONEXT;

                //CityName
                if (string.IsNullOrWhiteSpace(row.BIL_ID))
                {
                    errorMessage.Append("单据种类 - 不可空白. ");
                }
                zipCode.BIL_ID = row.BIL_ID;

                //Town
                if (string.IsNullOrWhiteSpace(row.MOB_ID))
                {
                    errorMessage.Append("模板名稱 - 不可空白. ");
                }
                zipCode.MOB_ID = row.MOB_ID;

                //=============================================================================
                if (errorMessage.Length > 0)
                {
                    errorCount += 1;
                    importErrorMessages.Add(string.Format(
                        "第 {0} 列資料發現錯誤：{1}{2}",
                        rowIndex,
                        errorMessage,
                        "<br/>"));
                }
                importZipCodes.Add(zipCode);
                rowIndex += 1;
            }

            try
            {
                result.ID = Guid.NewGuid();
                result.Success = errorCount.Equals(0);
                result.RowCount = importZipCodes.Count;
                result.ErrorCount = errorCount;

                string allErrorMessage = string.Empty;

                foreach (var message in importErrorMessages)
                {
                    allErrorMessage += message;
                }

                result.ErrorMessage = allErrorMessage;

                return result;
            }
            catch (Exception ex)
            {
                throw;
            }
        }


        /// <summary>
        /// Saves the import data.
        /// </summary>
        /// <param name="importZipCodes">The import zip codes.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void SaveImportData(IEnumerable<TF_SHLC> importZipCodes)
        {
            try
            {
                //先砍掉全部資料
                using (var db = new DB_HCTKEntities())
                {
                    foreach (var item in db.TF_SHLC.OrderBy(x => x.BIL_ID)) 
                    {
                        //db.TF_SHLC.Remove(item);// 不能用在数据库
                    }
                    //db.SaveChanges();
                }

                //再把匯入的資料給存到資料庫
                using (var db = new DB_HCTKEntities())
                {
                    foreach (var item in importZipCodes)
                    {
                        db.TF_SHLC.Add(item);
                    }
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}