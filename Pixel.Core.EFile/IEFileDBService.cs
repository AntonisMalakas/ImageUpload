using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Pixel.Core.EFile
{
    public interface IEFileDBService
    {
        Task<object> UploadFile(string collectionName, string filename, byte[] source, dynamic metadataOfUploadedFile);

        string UpdateFile(string collectionName, string mongoId, string fileName, string fileDescription, string fileDocumentType, string extension, string fileExpiryDate, int isSensitiveData);

        dynamic DownloadSingleFile(string collectionName, string mongoID);
        bool DeleteFile(string collectionName, string mongoID);
        bool DeleteFiles(string collectionName, List<string> mongoIDs);
        object DisplayMultipleFiles(string collectionName, string tableName, int tableKey, int profileId, bool canViewSensitiveData);
        Task<int> SaveScannedImage(string collectionName, dynamic imageData);

        List<dynamic> GetDocumentTypeList(string collectionName);
        Object GetDocumentTypeByID(string collectionName, string id);
        int AddNewDocumentType(string collectionName, dynamic payload);
        List<dynamic> UpdateDocumentType(string collectionName, dynamic payload);
        bool DeleteDocumentTypeById(string collectionName, int documentTypeId);

        string ReplaceEfilesTableName(string collectionName, int oldTableKey, string oldTableName, int newTableKey, string newTableName);

    }

}
