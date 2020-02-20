using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

namespace Pixel.Core.EFile
{
    public class EFileDBService : IEFileDBService
    {
        protected static IMongoClient _client;
        protected static IMongoDatabase _database;
        protected GridFSBucket _bucket;
        //private IConfiguration _configuration;

        public EFileDBService(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MongoDbConnection");
            _client = new MongoClient(connectionString);
            //_client = new MongoClient("mongodb://192.168.0.10:27017");
            //_client = new MongoClient();
            _database = _client.GetDatabase("EFileDb");

        }

        public int getNextSequence(string fieldName)
        {
            var countersCollection = _database.GetCollection<BsonDocument>("counters");
            var filter = Builders<BsonDocument>.Filter.Eq("_id", fieldName);
            var update = Builders<BsonDocument>.Update.Inc("seq", 1);
            var documentExist = countersCollection.Find(filter).ToList().Count != 0;
            if (documentExist)
            {
                var result = countersCollection.FindOneAndUpdate(filter, update);
                var res = result.GetElement("seq").Value.AsInt32;
                return res;
            };
            return -1;
        }


        public async Task<object> UploadFile(string collectionName, string filename, byte[] source, dynamic metadataOfUploadedFile)
        {
            dynamic objToReturn = new ExpandoObject();
            try
            {
                _bucket = new GridFSBucket(_database, new GridFSBucketOptions
                {
                    BucketName = collectionName,
                    ChunkSizeBytes = 1048576,
                    WriteConcern = WriteConcern.WMajority,
                    ReadPreference = ReadPreference.Secondary
                });

                var fileId = getNextSequence(collectionName + ".files");
                var options = new GridFSUploadOptions
                {

                    Metadata = new BsonDocument
                    {
                        {"fileId", fileId},
                        {"profileId",  metadataOfUploadedFile.profileId},
                        {"profileTypeId",  metadataOfUploadedFile.profileTypeId},
                        {"tableName", metadataOfUploadedFile.tableName },
                        {"tableNo", metadataOfUploadedFile.tableNo },
                        {"tableKey", metadataOfUploadedFile.tableKey },
                        {"detailInfo", metadataOfUploadedFile.detailInfo },
                        {"contentType", this._GetContentType(metadataOfUploadedFile.contentType.ToString()) },
                        {"description", metadataOfUploadedFile.description.ToString() },
                        {"documentType", metadataOfUploadedFile.documentType.ToString() },
                        {"expiryDate", metadataOfUploadedFile.expiryDate.ToString() },
                        {"isSensitiveData", Convert.ToInt32(metadataOfUploadedFile.isSensitiveData.ToString())},
                    }
                };
                var uploadId = _bucket.UploadFromBytes(filename, source, options);
                objToReturn.fileId = fileId;
                objToReturn.errorMessage = "";
                return objToReturn;
            }
            catch (Exception e)
            {
                objToReturn.fileId = -1;
                objToReturn.errorMessage = e.Message;
                return objToReturn;
            }
        }

        private string _GetContentType(dynamic contentTypeLong)
        {
            string excel1 = "application/vnd.ms-excel";
            string excel2 = "application/excel";
            string excel3 = "application/x-msexcel";
            string excel4 = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            string word1 = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

            if (contentTypeLong == excel1 || contentTypeLong == excel2 || contentTypeLong == excel3 || contentTypeLong == excel4)
            {
                return "excel";
            }
            if (contentTypeLong == "image/jpeg" || contentTypeLong == "image/png" || contentTypeLong == "image/gif")
            {
                return "image";
            }
            if (contentTypeLong == "video/mp4")
            {
                return "video";
            }
            if (contentTypeLong == "audio/mp3")
            {
                return "audio";
            }
            if (contentTypeLong == "application/pdf")
            {
                return "pdf";
            }
            if (contentTypeLong == word1)
            {
                return "word";
            }
            if (contentTypeLong == "text/plain")
            {
                return "text";
            }
            if (contentTypeLong == "application/octet-stream")
            {
                return "msg";
            }
            return "others"; // should not reach here.
        }

        public object DisplayMultipleFiles(string collectionName, string _tableType, int _tableId, int _profileId, bool canViewSensitiveData)
        {
            List<byte[]> byteArrayList = new List<byte[]>();
            List<dynamic> imageMetadataList = new List<dynamic>();

            _bucket = new GridFSBucket(_database, new GridFSBucketOptions
            {
                BucketName = collectionName,
                ChunkSizeBytes = 1048576,
                WriteConcern = WriteConcern.WMajority,
                ReadPreference = ReadPreference.Secondary
            });

            var filter = Builders<GridFSFileInfo>.Filter.Empty;

            if (_tableType == "Insured" || _tableType == "Profiles")
            {
                if (canViewSensitiveData)
                {
                    filter = Builders<GridFSFileInfo>.Filter.And(
                                   Builders<GridFSFileInfo>.Filter.Eq("metadata.profileId", _tableId)
                               );
                }
                else
                {
                    filter = Builders<GridFSFileInfo>.Filter.And(
                                   Builders<GridFSFileInfo>.Filter.Eq("metadata.profileId", _tableId),
                                   Builders<GridFSFileInfo>.Filter.Eq("metadata.isSensitiveData", 0)

                               );
                }
                //Builders<GridFSFileInfo>.Filter.Or(
                //    Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", "Policy"),
                //    Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", "Claim"),
                //    Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", "ClaimProfile"),
                //    Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", "Insured"),
                //    Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", "Profiles")
                //    )

            }
            else if (_tableType == "ClaimProfile")
            {
                if (canViewSensitiveData)
                {
                    //Builders<GridFSFileInfo>.Filter.Eq("metadata.typeId", _tableId)

                    filter = Builders<GridFSFileInfo>.Filter.And(
                                Builders<GridFSFileInfo>.Filter.Eq("metadata.profileId", _profileId),
                                Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", _tableType),
                                Builders<GridFSFileInfo>.Filter.Eq("metadata.tableKey", _tableId)
                                );
                }
                else
                {
                    filter = Builders<GridFSFileInfo>.Filter.And(
                               Builders<GridFSFileInfo>.Filter.Eq("metadata.profileId", _profileId),
                               Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", _tableType),
                               Builders<GridFSFileInfo>.Filter.Eq("metadata.tableKey", _tableId),
                               Builders<GridFSFileInfo>.Filter.Eq("metadata.isSensitiveData", 0)
                               );
                }
            }
            else if (_tableType == "Claim")
            {
                if (canViewSensitiveData)
                {
                    filter = Builders<GridFSFileInfo>.Filter.And(
                            //Builders<GridFSFileInfo>.Filter.Eq("metadata.type", _tableType),
                            //Builders<GridFSFileInfo>.Filter.Eq("metadata.typeId", _tableId)
                            Builders<GridFSFileInfo>.Filter.Eq("metadata.tableKey", _tableId),
                            Builders<GridFSFileInfo>.Filter.Or(
                            Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", _tableType),
                            Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", "ClaimProfile")
                                )
                            );
                }
                else
                {
                    filter = Builders<GridFSFileInfo>.Filter.And(
                            //Builders<GridFSFileInfo>.Filter.Eq("metadata.type", _tableType),
                            //Builders<GridFSFileInfo>.Filter.Eq("metadata.typeId", _tableId)
                            Builders<GridFSFileInfo>.Filter.Eq("metadata.tableKey", _tableId),
                            Builders<GridFSFileInfo>.Filter.Eq("metadata.isSensitiveData", 0),
                            Builders<GridFSFileInfo>.Filter.Or(
                               Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", _tableType),
                               Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", "ClaimProfile")
                              )
                            );
                }
            }
            else if (_tableType == "LifeClaim")
            {
                if (canViewSensitiveData)
                {
                    filter = Builders<GridFSFileInfo>.Filter.And(
                            //Builders<GridFSFileInfo>.Filter.Eq("metadata.type", _tableType),
                            //Builders<GridFSFileInfo>.Filter.Eq("metadata.typeId", _tableId)
                            Builders<GridFSFileInfo>.Filter.Eq("metadata.tableKey", _tableId),
                            Builders<GridFSFileInfo>.Filter.Or(
                            Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", _tableType),
                            Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", "ClaimProfile")
                                )
                            );
                }
                else
                {
                    filter = Builders<GridFSFileInfo>.Filter.And(
                            //Builders<GridFSFileInfo>.Filter.Eq("metadata.type", _tableType),
                            //Builders<GridFSFileInfo>.Filter.Eq("metadata.typeId", _tableId)
                            Builders<GridFSFileInfo>.Filter.Eq("metadata.tableKey", _tableId),
                            Builders<GridFSFileInfo>.Filter.Eq("metadata.isSensitiveData", 0),
                            Builders<GridFSFileInfo>.Filter.Or(
                               Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", _tableType),
                               Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", "ClaimProfile")
                              )
                            );
                }
            }
            else if (_tableType == "Transaction" || _tableType == "Account" || _tableType == "Policy" || _tableType == "Proposal" || _tableType == "CoverNote"
                                                 || _tableType == "LifePolicy" || _tableType == "LifeProposal" || _tableType == "Asset" || _tableType == "Purchase")
            {
                if (canViewSensitiveData)
                {

                    filter = Builders<GridFSFileInfo>.Filter.And(
                                    Builders<GridFSFileInfo>.Filter.Eq("metadata.tableKey", _tableId),
                                    Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", _tableType)
                              );
                }
                else
                {
                    filter = Builders<GridFSFileInfo>.Filter.And(
                                    Builders<GridFSFileInfo>.Filter.Eq("metadata.tableKey", _tableId),
                                    Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", _tableType),
                                    Builders<GridFSFileInfo>.Filter.Eq("metadata.isSensitiveData", 0)
                              );
                }
            }

            else
            {
                if (canViewSensitiveData)
                {
                    filter = Builders<GridFSFileInfo>.Filter.And(
                                 //Builders<GridFSFileInfo>.Filter.Eq("metadata.type", _tableType),
                                 //Builders<GridFSFileInfo>.Filter.Eq("metadata.typeId", _tableId)
                                 Builders<GridFSFileInfo>.Filter.Or(
                                 Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", _tableType),
                                 Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", "ClaimProfile")
                               ),
                           //Builders<GridFSFileInfo>.Filter.Eq("metadata.tableKey", _tableId)
                           Builders<GridFSFileInfo>.Filter.Eq("metadata.profileId", _profileId)

                           );
                }
                else
                {
                    filter = Builders<GridFSFileInfo>.Filter.And(
                            Builders<GridFSFileInfo>.Filter.Or(
                            Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", _tableType),
                            Builders<GridFSFileInfo>.Filter.Eq("metadata.tableName", "ClaimProfile")
                          ),
                      Builders<GridFSFileInfo>.Filter.Eq("metadata.profileId", _profileId),
                      Builders<GridFSFileInfo>.Filter.Eq("metadata.isSensitiveData", 0)
                      );
                }
            }



            var targetFileList = _bucket.Find(filter).ToList();
            var filesToDisplay = MapFilesData(targetFileList);
            var grouppedFiles = _GroupFilesByTypeAndTypeId(filesToDisplay);
            return grouppedFiles;
        }

        private object _GroupFilesByTypeAndTypeId(List<dynamic> filesToDisplay)
        {
            var objectsbyDate = filesToDisplay
                                            //.GroupBy(x => new { x.type, x.typeId })
                                            .GroupBy(x => new { x.tableName, x.tableKey, x.profileId })

                                            .Select(g => g.ToList())
                                            .ToList();

            return objectsbyDate;
        }


        private List<object> MapFilesData(List<GridFSFileInfo> targetFileList)
        {
            List<object> metadataList = new List<object>();
            foreach (var targetFile in targetFileList)
            {
                var fileSize = ConvertToBytes(targetFile.Length);
                var contentTypeMetadata = targetFile.Metadata["contentType"].ToString();
                var mongoIdMetadata = targetFile.Id.ToString();
                var descriptionMetadata = targetFile.Metadata["description"].ToString();
                var profileId = targetFile.Metadata["profileId"].ToString();
                var profileTypeId = targetFile.Metadata["profileTypeId"].ToString();
                //var type = targetFile.Metadata["type"].ToString();
                //var typeId = targetFile.Metadata["typeId"].ToString();
                //var typeNumber = targetFile.Metadata["typeNumber"].ToString();
                var tableName = targetFile.Metadata["tableName"].ToString();
                var tableNo = targetFile.Metadata["tableNo"].ToString();
                var tableKey = targetFile.Metadata["tableKey"].ToString();
                var detailInfo = targetFile.Metadata["detailInfo"].ToString();
                var documentType = targetFile.Metadata["documentType"].ToString();
                var expiryDate = targetFile.Metadata["expiryDate"].ToString();
                var isSensitiveData = Convert.ToInt32(targetFile.Metadata["isSensitiveData"].ToString());


                BsonValue fileId = targetFile.Id;

                dynamic fileMetadata = new ExpandoObject();
                fileMetadata.fileName = targetFile.Filename;
                fileMetadata.uploadedDateTime = targetFile.UploadDateTime;
                fileMetadata.fileSize = fileSize;
                fileMetadata.mongoId = mongoIdMetadata;// possibility of error
                fileMetadata.contentType = contentTypeMetadata;
                fileMetadata.description = descriptionMetadata;
                fileMetadata.profileId = profileId;
                fileMetadata.profileTypeId = profileTypeId;
                //fileMetadata.type = tableName;
                //fileMetadata.typeId = tableKey;
                //fileMetadata.typeNumber = detailInfo;
                fileMetadata.tableName = tableName;
                fileMetadata.tableNo = tableNo;
                fileMetadata.tableKey = tableKey;
                fileMetadata.detailInfo = detailInfo;
                fileMetadata.documentType = documentType;
                fileMetadata.expiryDate = expiryDate;
                fileMetadata.isSensitiveData = isSensitiveData;


                if (contentTypeMetadata == "image")
                {
                    var bytes = _bucket.DownloadAsBytes(fileId);
                    fileMetadata.byteArray = bytes;
                }
                else
                {
                    fileMetadata.byteArray = null;
                }
                metadataList.Add(fileMetadata);
            }
            return metadataList;
        }

        private dynamic _SplitByLastDot(string filename)
        {
            int index = filename.LastIndexOf('.');
            var slicedFileName = filename.Slice(0, index);

            return slicedFileName;
        }

        public string ConvertToBytes(long source)
        {
            return ToFileSize(Convert.ToInt64(source));
        }

        public string ToFileSize(long source)
        {
            const int byteConversion = 1024;
            double bytes = Convert.ToDouble(source);

            if (bytes >= Math.Pow(byteConversion, 3)) //GB Range
            {
                return string.Concat(Math.Round(bytes / Math.Pow(byteConversion, 3), 2), " GB");
            }
            else if (bytes >= Math.Pow(byteConversion, 2)) //MB Range
            {
                return string.Concat(Math.Round(bytes / Math.Pow(byteConversion, 2), 2), " MB");
            }
            else if (bytes >= byteConversion) //KB Range
            {
                return string.Concat(Math.Round(bytes / byteConversion, 2), " KB");
            }
            else //Bytes
            {
                return string.Concat(bytes, " Bytes");
            }
        }


        public dynamic DownloadSingleFile(string collectionName, string mongoID)
        {
            try
            {


                List<dynamic> imageMetadataList = new List<dynamic>();

                _bucket = new GridFSBucket(_database, new GridFSBucketOptions
                {
                    BucketName = collectionName,
                    ChunkSizeBytes = 1048576,
                    WriteConcern = WriteConcern.WMajority,
                    ReadPreference = ReadPreference.Secondary
                });

                var filter = Builders<GridFSFileInfo>.Filter.Eq("_id", new ObjectId(mongoID));
                //var filter = Builders<GridFSFileInfo>.Filter.Eq("files_id", mongoID);
                //var filter = new BsonDocument();
                var targetFileList = _bucket.Find(filter).ToList();


                var filesMetadata = MapFilesData(targetFileList);

                if (targetFileList == null || targetFileList.Count == 0)
                {
                    return null;
                }
                else
                {
                    foreach (var targetFile in targetFileList)
                    {
                        if (targetFile.Id == new ObjectId(mongoID))
                        {
                            var bytes = _bucket.DownloadAsBytes(targetFile.Id);
                            dynamic imageAndData = new ExpandoObject();
                            imageAndData.byteArray = bytes;
                            imageAndData.fileName = targetFile.Filename;
                            imageAndData.contentType = targetFile.Metadata["contentType"].ToString();


                            imageMetadataList.Add(imageAndData);
                            return imageAndData;


                        }
                    }
                    return null;

                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }



        public bool DeleteFile(string collectionName, string mongoID)
        {
            _bucket = new GridFSBucket(_database, new GridFSBucketOptions
            {
                BucketName = collectionName,
                ChunkSizeBytes = 1048576,
                WriteConcern = WriteConcern.WMajority,
                ReadPreference = ReadPreference.Secondary
            });

            var filter = new BsonDocument();
            var targetFileList = _bucket.Find(filter).ToList();
            if (targetFileList == null || targetFileList.Count == 0)
            {
                return false;
            }
            else
            {
                foreach (var targetFile in targetFileList)
                {
                    if (targetFile.Id == new ObjectId(mongoID))
                    {
                        _bucket.Delete(targetFile.Id);
                        return true;
                    }
                }
                return false;
            }
        }

        public bool DeleteFiles(string collectionName, List<string> mongoIDs)
        {
            _bucket = new GridFSBucket(_database, new GridFSBucketOptions
            {
                BucketName = collectionName,
                ChunkSizeBytes = 1048576,
                WriteConcern = WriteConcern.WMajority,
                ReadPreference = ReadPreference.Secondary
            });


            foreach (var mongoId in mongoIDs)
            {

                var filter = Builders<GridFSFileInfo>.Filter.Eq("_id", new ObjectId(mongoId));
                //var filter = new BsonDocument();
                var targetFileList = _bucket.Find(filter).ToList();
                if (targetFileList == null || targetFileList.Count == 0)
                {
                    return false;
                }
                else
                {
                    foreach (var targetFile in targetFileList)
                    {
                        foreach (var mongoID in mongoIDs)
                        {
                            if (targetFile.Id == new ObjectId(mongoID))
                            {
                                _bucket.Delete(targetFile.Id);
                            }
                        }

                    }
                }
            }
            return true;

        }

        public async Task<int> SaveScannedImage(string collectionName, dynamic imageData)
        {
            var base64String = imageData.base64Value.ToString();
            string base64CleanedString = base64String.Replace("data:image/png;base64,", "");
            byte[] imageBytes = Convert.FromBase64String(base64CleanedString);


            return 0;
        }

        public List<dynamic> GetDocumentTypeList(string collectionName)
        {
            var targetCollection = _database.GetCollection<BsonDocument>(collectionName);
            var filter = new BsonDocument();
            var result = targetCollection.Find(filter)
                 .Project(Builders<BsonDocument>.Projection.Exclude("_id"))
                 .ToList()
                 .ToJson();

            var deserializeddocumentTypeList = JsonConvert.DeserializeObject<List<dynamic>>(result);
            return deserializeddocumentTypeList;
        }

        public int AddNewDocumentType(string collectionName, dynamic payload)
        {
            int documentTypesId;

            // payload = { description: "testDocumentType" }
            dynamic element = new ExpandoObject();
            element = payload;
            int id = -1;

            id = _GetNextSequence(collectionName);
            if (id != -1)
            {
                element.id = id.ToString();
            }

            var jsonDoc = Newtonsoft.Json.JsonConvert.SerializeObject(element);
            var bsonDoc = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonDocument>(jsonDoc);

            var tagetCollection = _database.GetCollection<BsonDocument>(collectionName);
            tagetCollection.InsertOne(bsonDoc);
            documentTypesId = id;

            return documentTypesId;
        }

        public List<dynamic> UpdateDocumentType(string collectionName, dynamic payload)
        {
            List<dynamic> resultList = new List<dynamic>();



            dynamic element = new ExpandoObject();
            element = payload;
            string id = element.id;


            var targetCollection = _database.GetCollection<BsonDocument>(collectionName);
            var filter = Builders<BsonDocument>.Filter.Eq("id", id);

            var jsonDoc = JsonConvert.SerializeObject(payload);
            var bsonDoc = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonDocument>(jsonDoc);
            var result = targetCollection.ReplaceOne(filter, bsonDoc);
            resultList.Add(result.MatchedCount > 0 && result.ModifiedCount > 0);

            return resultList;
        }

        public bool DeleteDocumentTypeById(string collectionName, int documentTypeId)
        {
            try
            {
                var targetCollection = _database.GetCollection<BsonDocument>(collectionName);
                var filter = Builders<BsonDocument>.Filter.Regex("id", new BsonRegularExpression(documentTypeId.ToString()));
                var result = targetCollection.DeleteOne(filter);
                if (result.DeletedCount > 0)
                    return true;
                return false;
            }
            catch (Exception e)
            {
                return false;
            }
        }


        private int _GetNextSequence(string fieldName)
        {
            var countersCollection = _database.GetCollection<BsonDocument>("counters");
            var filter = Builders<BsonDocument>.Filter.Eq("_id", fieldName);
            var update = Builders<BsonDocument>.Update.Inc("seq", 1);
            var documentExist = countersCollection.Find(filter).ToList().Count != 0;
            if (documentExist)
            {
                var result = countersCollection.FindOneAndUpdate(filter, update);
                var res = result.GetElement("seq").Value.AsInt32;
                return res;
            };
            return -1;
        }

        public string UpdateFile(string collectionName, string mongoId, string fileName, string fileDescription, string fileDocumentType, string extension, string fileExpiryDate, int isSensitiveData)
        {
            _bucket = new GridFSBucket(_database, new GridFSBucketOptions
            {
                BucketName = collectionName,
                ChunkSizeBytes = 1048576,
                WriteConcern = WriteConcern.WMajority,
                ReadPreference = ReadPreference.Secondary
            });

            var filter1 = Builders<GridFSFileInfo>.Filter.Eq("_id", ObjectId.Parse(mongoId));
            var targetFileList = _bucket.Find(filter1).ToJson();

            string fileNameWithExtension = fileName + "." + extension;
            //int fileDocumentTypeInt = Convert.ToInt32(fileDocumentType);

            var targetCollection = _database.GetCollection<BsonDocument>(collectionName + ".files");
            var filter = Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(mongoId));
            var update = Builders<BsonDocument>.Update.Set("filename", fileNameWithExtension)
                                                      .Set("metadata.description", fileDescription)
                                                      .Set("metadata.documentType", fileDocumentType)
                                                      .Set("metadata.expiryDate", fileExpiryDate)
                                                      .Set("metadata.isSensitiveData", isSensitiveData);

            var documentExist = targetCollection.Find(filter).ToList().Count != 0;
            if (documentExist)
            {
                var result = targetCollection.FindOneAndUpdate(filter, update);
                return "Success";
            }
            else
            {
                return "Error";
            }

        }

        public object GetDocumentTypeByID(string collectionName, string id)
        {

            try
            {
                var targetCollection = _database.GetCollection<BsonDocument>(collectionName);
                var filter = Builders<BsonDocument>.Filter.Regex("id", new BsonRegularExpression(id.ToString()));
                var result = targetCollection.Find(filter)
                     .Project(Builders<BsonDocument>.Projection.Exclude("_id"))

                     .ToList()
                     .ToJson();
                var deserializedDocumentTypeList = JsonConvert.DeserializeObject<List<object>>(result);
                List<object> documentTypeList = new List<object>();

                foreach (dynamic workflow in deserializedDocumentTypeList)
                {

                    dynamic obj = new ExpandoObject();
                    obj.id = Convert.ToInt32(workflow.id);
                    obj.description = workflow.description;
                    obj.is_document_expiry_required = workflow.is_document_expiry_required;


                    documentTypeList.Add(obj);

                }
                return documentTypeList[0];
            }
            catch (Exception e)
            {
                return new object();
            }
        }

        public string ReplaceEfilesTableName(string collectionName, int oldTableKey, string oldTableName, int newTableKey, string newTableName)
        {
            try
            {
                var targetCollection = _database.GetCollection<BsonDocument>(collectionName + ".files");
                var filter = Builders<BsonDocument>.Filter.And(
                           Builders<BsonDocument>.Filter.Eq("metadata.tableKey", oldTableKey),
                           Builders<BsonDocument>.Filter.Eq("metadata.tableName", oldTableName)
                     );
                var update = Builders<BsonDocument>.Update
                                                           .Set("metadata.tableKey", newTableKey)
                                                           .Set("metadata.tableName", newTableName);

                var documentExist = targetCollection.Find(filter).ToList().Count != 0;
                if (documentExist)
                {
                    //var result = targetCollection.FindOneAndUpdate(filter, update);
                    var res = targetCollection.UpdateMany(filter, update);
                    if (res.IsAcknowledged)
                    {
                        return "Success";
                    }
                    else
                    {
                        return "Error: Cannot Update EFiles";
                    }
                }
                else
                {
                    return "Error: Cannot Update EFiles";
                }

            }
            catch (Exception e)
            {
                return "Error: Cannot Update EFiles";
            }
        }
    }
}

public class MongoId
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    public int id { get; set; }
    public string name { get; set; }
}

public static class Extensions
{
    /// <summary>
    /// Get the string slice between the two indexes.
    /// Inclusive for start index, exclusive for end index.
    /// </summary>
    public static string Slice(this string source, int start, int end)
    {
        if (end < 0) // Keep this for negative end support
        {
            end = source.Length + end;
        }
        int len = end - start;               // Calculate length
        return source.Substring(start, len); // Return Substring of length
    }
}


