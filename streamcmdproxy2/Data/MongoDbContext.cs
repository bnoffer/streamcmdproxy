using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using streamcmdproxy2.Data.Convention;
using streamcmdproxy2.Data.Models;

namespace streamcmdproxy2.Data
{
	public class MongoDbContext : IDisposable
	{
        #region Singleton

        private static MongoDbContext _instance;
        private static object _syncRoot = new object();
        public static MongoDbContext Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_syncRoot)
                    {
                        _instance = new MongoDbContext();
                    }
                }

                return _instance;
            }
        }

        static string ConnectionString;
        static string DatabaseName;

        public static void Init(string connectionString, string databaseName)
        {
            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(databaseName))
                throw new ArgumentNullException("You need to specify the connection string and database name.");

            ConnectionString = connectionString;
            DatabaseName = databaseName;
        }

        public static void Reset()
        {
            _instance = null;
        }

        #endregion

        private MongoDbContext()
        {
            ConventionRegistry.Register(
                "DictionaryRepresentationConvention",
                new ConventionPack { new DictionaryRepresentationConvention(DictionaryRepresentation.ArrayOfArrays) },
                _ => true);

            DatabaseSetup();
        }

        private async void DatabaseSetup()
        {
            var tag = this + ".DatabaseSetup";
            try
            {
                // Check if DB is configured
                if (string.IsNullOrEmpty(ConnectionString) || string.IsNullOrEmpty(DatabaseName))
                    throw new ArgumentNullException("Database has not been configured, please call Init(connectionString, databaseName) first.");

                var client = new MongoClient(ConnectionString);

                // Make sure our Database exists
                var db = client.GetDatabase(DatabaseName);

                // Make sure our Document Collection exists
                await CreateCollectionIfNotExists(MongoDbCollections.UserCollection);
                await CreateCollectionIfNotExists(MongoDbCollections.CommandCollection);
                await CreateCollectionIfNotExists(MongoDbCollections.ConfigCollection);

                Task.Factory.StartNew(CheckIndexes);
            }
            catch (Exception ex)
            {
                Track.Error(tag, ex.Message + "\r\nStackTrace:\r\n" + ex.StackTrace);
            }
        }

        public void CreateDocumentIfNotExists<T>(string collection, T document) where T : BaseModel
        {
            var tag = this + ".CreateDocumentIfNotExists";
            try
            {
                // Get Client, DB and Collection
                var client = new MongoClient(ConnectionString);
                var db = client.GetDatabase(DatabaseName);
                var col = db.GetCollection<T>(collection);

                // Check if the item already exists, if not upload it
                var result = from d in col.AsQueryable() where d.DocumentId.Equals(document.DocumentId) select d;
                if (!result.Any())
                    col.InsertOne(document);
            }
            catch (Exception ex)
            {
                Track.Error(tag, ex.Message + "\r\nStackTrace:\r\n" + ex.StackTrace);
            }
        }

        public void ReplaceDocument<T>(string collection, string documentId, T updatedDocument) where T : BaseModel
        {
            var tag = this + ".ReplaceDocument";
            try
            {
                // Get Client, DB and Collection
                var client = new MongoClient(ConnectionString);
                var db = client.GetDatabase(DatabaseName);
                var col = db.GetCollection<T>(collection);

                var old = from d in col.AsQueryable() where d.DocumentId.Equals(documentId) select d;
                updatedDocument._id = old.First()._id;
                col.ReplaceOne<T>(d => d.DocumentId.Equals(documentId), updatedDocument);
            }
            catch (Exception ex)
            {
                Track.Error(tag, ex.Message + "\r\nStackTrace:\r\n" + ex.StackTrace);
            }
        }

        public void DeleteDocument<T>(string collection, string documentId) where T : BaseModel
        {
            var tag = this + ".DeleteDocument";
            try
            {
                // Get Client, DB and Collection
                var client = new MongoClient(ConnectionString);
                var db = client.GetDatabase(DatabaseName);
                var col = db.GetCollection<T>(collection);

                col.DeleteOne(d => d.DocumentId.Equals(documentId));
            }
            catch (Exception ex)
            {
                Track.Error(tag, ex.Message + "\r\nStackTrace:\r\n" + ex.StackTrace);
            }
        }

        public void DeleteMany<T>(string collection, System.Linq.Expressions.Expression<System.Func<T, bool>> expression) where T : BaseModel
        {
            var tag = this + ".DeleteMany";
            try
            {
                // Get Client, DB and Collection
                var client = new MongoClient(ConnectionString);
                var db = client.GetDatabase(DatabaseName);
                var col = db.GetCollection<T>(collection);

                col.DeleteMany<T>(expression);
            }
            catch (Exception ex)
            {
                Track.Error(tag, ex.Message + "\r\nStackTrace:\r\n" + ex.StackTrace);
            }
        }

        public List<T> GetDocuments<T>(string collection) where T : BaseModel
        {
            var tag = this + ".GetDocument";
            try
            {
                // Get Client, DB and Collection
                var client = new MongoClient(ConnectionString);
                var db = client.GetDatabase(DatabaseName);
                var col = db.GetCollection<T>(collection);

                var result = from d in col.AsQueryable() select d;
                return result.ToList();
            }
            catch (Exception ex)
            {
                Track.Error(tag, ex.Message + "\r\nStackTrace:\r\n" + ex.StackTrace);
                return new List<T>();
            }
        }

        public IQueryable<T> GetQueryableCollection<T>(string collection)
        {
            var tag = this + ".GetQueryableCollection";
            try
            {
                // Get Client, DB and Collection
                var client = new MongoClient(ConnectionString);
                var db = client.GetDatabase(DatabaseName);
                return db.GetCollection<T>(collection).AsQueryable(new AggregateOptions { AllowDiskUse = true });
            }
            catch (Exception ex)
            {
                Track.Error(tag, ex.Message + "\r\nStackTrace:\r\n" + ex.StackTrace);
                return null;
            }
        }

        public T GetDocument<T>(string collection, string documentId) where T : BaseModel
        {
            var tag = this + ".GetDocument";
            try
            {
                // Get Client, DB and Collection
                var client = new MongoClient(ConnectionString);
                var db = client.GetDatabase(DatabaseName);
                var col = db.GetCollection<T>(collection);

                // Check if the item already exists, if not upload it
                var result = from d in col.AsQueryable() where d.DocumentId.Equals(documentId) select d;
                return result.First();
            }
            catch (Exception ex)
            {
                Track.Error(tag, ex.Message + "\r\nStackTrace:\r\n" + ex.StackTrace);
                return default(T);
            }
        }

        public void UploadFile(string filename, byte[] data)
        {
            var tag = this + ".UploadFile";
            try
            {
                // Get Client, DB and Bucket
                var client = new MongoClient(ConnectionString);
                var db = client.GetDatabase(DatabaseName);
                var bucket = new GridFSBucket(db);
                bucket.UploadFromBytes(filename, data);
            }
            catch (Exception ex)
            {
                Track.Error(tag, ex.Message + "\r\nStackTrace:\r\n" + ex.StackTrace);
                return;
            }
        }

        public byte[] DownloadFile(string filename)
        {
            var tag = this + ".DownloadFile";
            try
            {
                // Get Client, DB and Bucket
                var client = new MongoClient(ConnectionString);
                var db = client.GetDatabase(DatabaseName);
                var bucket = new GridFSBucket(db);
                return bucket.DownloadAsBytesByName(filename);
            }
            catch (Exception ex)
            {
                Track.Error(tag, ex.Message + "\r\nStackTrace:\r\n" + ex.StackTrace);
                return null;
            }
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// Creates a new collection inside of the database if it does not already exist
        /// </summary>
        /// <param name="collectionName">Name of the new collection</param>
        async Task CreateCollectionIfNotExists(string collectionName)
        {
            var client = new MongoClient(ConnectionString);
            var db = client.GetDatabase(DatabaseName);

            var result = await db.ListCollectionNamesAsync();
            var collections = result.ToList();
            if (!collections.Contains(collectionName))
                db.CreateCollection(collectionName);
        }

        private void CheckIndexes()
        {
            CheckIndex<User>(MongoDbCollections.UserCollection);
            CheckIndex<Command>(MongoDbCollections.CommandCollection);
            CheckIndex<Config>(MongoDbCollections.ConfigCollection);
            //CheckAnalyticsDataSetIndex();
            //CheckCouponDetailsIndex();
        }

        private async void CheckIndex<T>(string collection) where T : BaseModel
        {
            var tag = this + ".CheckIndex";
            try
            {
                Track.Info(tag, $"Checking index for collection {collection}");
                // Get Client, DB and Collection
                var client = new MongoClient(ConnectionString);
                var db = client.GetDatabase(DatabaseName);
                var col = db.GetCollection<T>(collection);

                var builder = Builders<T>.IndexKeys;
                var indexModel = new CreateIndexModel<T>(builder.Ascending(x => x.DocumentId));
                await col.Indexes.CreateOneAsync(indexModel);
            }
            catch (Exception ex)
            {
                Track.Error(tag, ex.Message + "\r\nStackTrace:\r\n" + ex.StackTrace);
            }
        }

        //private async void CheckCouponDetailsIndex()
        //{
        //    var tag = this + ".CheckCouponDetailsIndex";
        //    try
        //    {
        //        Track.Info(tag, $"Checking index for CouponDetails");
        //        // Get Client, DB and Collection
        //        var client = new MongoClient(ConnectionString);
        //        var db = client.GetDatabase(DatabaseName);
        //        var col = db.GetCollection<CouponDetails>(MongoDbCollections.CouponDetailsCollection);

        //        var builder = Builders<CouponDetails>.IndexKeys;
        //        var indexModel = new CreateIndexModel<CouponDetails>(builder.Ascending(x => x.CouponId));
        //        await col.Indexes.CreateOneAsync(indexModel);
        //        indexModel = new CreateIndexModel<CouponDetails>(builder.Ascending(x => x.ShortenedCouponId));
        //        await col.Indexes.CreateOneAsync(indexModel);
        //    }
        //    catch (Exception ex)
        //    {
        //        Track.Error(tag, ex.Message + "\r\nStackTrace:\r\n" + ex.StackTrace);
        //    }
        //}

        //private async void CheckAnalyticsDataSetIndex()
        //{
        //    var tag = this + ".CheckAnalyticsDataSetIndex";
        //    try
        //    {
        //        Track.Info(tag, $"Checking index for AnalyticsDataSet");
        //        // Get Client, DB and Collection
        //        var client = new MongoClient(ConnectionString);
        //        var db = client.GetDatabase(DatabaseName);
        //        var col = db.GetCollection<AnalyticsDataSet>(MongoDbCollections.AnalyticsDataCollection);

        //        var builder = Builders<AnalyticsDataSet>.IndexKeys;
        //        var indexModel = new CreateIndexModel<AnalyticsDataSet>(builder.Ascending(x => x.AppId).Ascending(x => x.Platform).Descending(x => x.Timestamp));
        //        await col.Indexes.CreateOneAsync(indexModel);
        //    }
        //    catch (Exception ex)
        //    {
        //        Track.Error(tag, ex.Message + "\r\nStackTrace:\r\n" + ex.StackTrace);
        //    }
        //}
    }
}

