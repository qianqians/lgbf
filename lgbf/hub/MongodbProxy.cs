using MongoDB.Driver;
namespace hub;

public class MongodbProxy
{
    private readonly MongoClient _client;

    public MongodbProxy(string ip, short port)
	{
        var setting = new MongoClientSettings()
        {
            Server = new MongoServerAddress(ip, port)
        };
        _client = new MongoClient(setting);
    }

    public MongodbProxy(string url)
    {
        var mongoUrl = new MongoUrl(url);
        _client = new MongoClient(mongoUrl);
    }

    private MongoClient GetMongoClient()
    {
        return _client;
    }

    public void CreateIndex(string db, string collection, string key, bool isUnique)
    {
        var mongoClient = GetMongoClient();
        var dbInst = mongoClient.GetDatabase(db);
        var collectionInst = dbInst.GetCollection<MongoDB.Bson.BsonDocument>(collection);

        try
        {
            var builder = new IndexKeysDefinitionBuilder<MongoDB.Bson.BsonDocument>();
            var opt = new CreateIndexOptions
            {
                Unique = isUnique
            };
            var indexModel = new CreateIndexModel<MongoDB.Bson.BsonDocument>(builder.Ascending(key), opt);
            collectionInst.Indexes.CreateOne(indexModel);
        }
        catch(System.Exception e)
        {
            Log.Err("create_index failed, {0}", e.Message);
        }
    }

    public async Task CheckIntGuid(string db, string collection, long guid)
    {
        var mongoClient = GetMongoClient();
        var dbInst = mongoClient.GetDatabase(db);
        var collectionInst = dbInst.GetCollection<MongoDB.Bson.BsonDocument>(collection);

        try
        {
            var bsonQuery = MongoDB.Bson.BsonDocument.Parse("{\"Guid\":\"__guid__\"}");
            var query = new BsonDocumentFilterDefinition<MongoDB.Bson.BsonDocument>(bsonQuery);

            var c = await collectionInst.FindAsync<MongoDB.Bson.BsonDocument>(query);
            if (c != null && await c.MoveNextAsync() && (c.Current == null || !c.Current.Any()))
            {
                MongoDB.Bson.BsonDocument d = new MongoDB.Bson.BsonDocument { { "Guid", "__guid__" }, { "inside_guid", guid } };
                await collectionInst.InsertOneAsync(d);
            }
        }
        catch (Exception e)
        {
            Log.Err("check_int_guid db: {0}, collection: {1}, inside_guid: {2}, faild: {3}", db, collection, guid, e);
        }
    }

    public async ValueTask<bool> Save(string db, string collection, byte[] bsonData)
    {
        var mongoClient = GetMongoClient();
        var dbInst = mongoClient.GetDatabase(db);
        var collectionInst = dbInst.GetCollection<MongoDB.Bson.BsonDocument>(collection);

        var d = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<MongoDB.Bson.BsonDocument>(bsonData);
        await collectionInst.InsertOneAsync(d);

        return true;
	}

    public async ValueTask<bool> Update(string db, string collection, byte[] bsonQuery, byte[] bsonUpdate, bool upsert)
    {
        var mongoClient = GetMongoClient();
        var dbInst = mongoClient.GetDatabase(db);
        var collectionInst = dbInst.GetCollection<MongoDB.Bson.BsonDocument>(collection);

        var bsonQueryDoc = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<MongoDB.Bson.BsonDocument>(bsonQuery);
        var bsonUpdateDoc = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<MongoDB.Bson.BsonDocument>(bsonUpdate);

        var query = new BsonDocumentFilterDefinition<MongoDB.Bson.BsonDocument>(bsonQueryDoc);
        var update = new BsonDocumentUpdateDefinition<MongoDB.Bson.BsonDocument>(bsonUpdateDoc);
        var options = new UpdateOptions() { IsUpsert = upsert };

        await collectionInst.UpdateOneAsync(query, update, options);

        return true;
	}

    public async ValueTask<MongoDB.Bson.BsonDocument> FindAndModify(string db, string collection, byte[] bsonQuery, byte[] bsonUpdate, bool isNew, bool upsert)
    {
        var mongoClient = GetMongoClient();
        var dbInst = mongoClient.GetDatabase(db);
        var collectionInst = dbInst.GetCollection<MongoDB.Bson.BsonDocument>(collection) as MongoDB.Driver.IMongoCollection<MongoDB.Bson.BsonDocument>;

        var bsonQueryDoc = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<MongoDB.Bson.BsonDocument>(bsonQuery);
        var bsonUpdateDoc = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<MongoDB.Bson.BsonDocument>(bsonUpdate);

        var query = new BsonDocumentFilterDefinition<MongoDB.Bson.BsonDocument>(bsonQueryDoc);
        var bsonUpdateImpl = new MongoDB.Bson.BsonDocument { { "$set", bsonUpdateDoc } };
        var update = new BsonDocumentUpdateDefinition<MongoDB.Bson.BsonDocument>(bsonUpdateImpl);
        var options = new FindOneAndUpdateOptions<MongoDB.Bson.BsonDocument, MongoDB.Bson.BsonDocument>()
        {
            ReturnDocument = isNew ? ReturnDocument.After : ReturnDocument.Before,
            IsUpsert = upsert
        };

        var r = await collectionInst.FindOneAndUpdateAsync(query, update, options);

        return r;
    }

    public async ValueTask<IAsyncCursor<MongoDB.Bson.BsonDocument>> Find(string db, string collection, byte[] bsonQuery, int skip, int limit, string sort, bool ascending)
    {
        var mongoClient = GetMongoClient();
        var dbInst = mongoClient.GetDatabase(db);
        var collectionInst = dbInst.GetCollection<MongoDB.Bson.BsonDocument>(collection);

        var bsonQueryDoc = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<MongoDB.Bson.BsonDocument>(bsonQuery);
        var opt = new FindOptions<MongoDB.Bson.BsonDocument>();
        if (skip > 0)
        {
            opt.Skip = skip;
        }
        if (limit > 0)
        {
            opt.Limit = limit;
        }
        if (!string.IsNullOrEmpty(sort))
        {
            if (ascending)
            {
                opt.Sort = Builders<MongoDB.Bson.BsonDocument>.Sort.Ascending(sort);
            }
            else
            {
                opt.Sort = Builders<MongoDB.Bson.BsonDocument>.Sort.Descending(sort);
            }
        }

        return await collectionInst.FindAsync<MongoDB.Bson.BsonDocument>(bsonQueryDoc, opt);
    }

    public async ValueTask<int> Count(string db, string collection, byte[] bsonQuery)
    {
        var mongoClient = GetMongoClient();
        var dbInst = mongoClient.GetDatabase(db);
        var collectionInst = dbInst.GetCollection<MongoDB.Bson.BsonDocument>(collection);

        var bsonQueryDoc = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<MongoDB.Bson.BsonDocument>(bsonQuery);
        return (int)(await collectionInst.CountDocumentsAsync(bsonQueryDoc));
    }

	public async ValueTask<bool> Remove(string db, string collection, byte[] bsonQuery)
    {
        var mongoClient = GetMongoClient();
        var dbInst = mongoClient.GetDatabase(db);
        var collectionInst = dbInst.GetCollection<MongoDB.Bson.BsonDocument>(collection);

        var bsonQueryDoc = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<MongoDB.Bson.BsonDocument>(bsonQuery);
        await collectionInst.DeleteOneAsync(bsonQueryDoc);

        return true;
	}

    public async ValueTask<long> GetGuid(string db, string collection)
    {
        var mongoClient = GetMongoClient();
        var dbInst = mongoClient.GetDatabase(db);
        var collectionInst = dbInst.GetCollection<MongoDB.Bson.BsonDocument>(collection);

        var query = new MongoDB.Bson.BsonDocument("Guid", "__guid__");
        var queryDoc = new BsonDocumentFilterDefinition<MongoDB.Bson.BsonDocument>(query);
        var bsonUpdateImpl = new MongoDB.Bson.BsonDocument { { "$inc", new MongoDB.Bson.BsonDocument { { "inside_guid", 1 } } } };

        var c = await collectionInst.FindOneAndUpdateAsync<MongoDB.Bson.BsonDocument>(queryDoc, bsonUpdateImpl);
        return c.GetValue("inside_guid").ToInt64();
    }
}

