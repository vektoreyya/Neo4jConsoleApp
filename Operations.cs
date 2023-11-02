using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;
using Neo4jClient;
using Neo4jClient.Cypher;
using Neo4j.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MongoDBConsoleApp
{
    public class Operations
    {
        public static string ConnectionString
        {
            get
            {
                return new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetConnectionString("SN");
            }
        }
        private IMongoClient client;
        private IMongoDatabase database;
        private IMongoCollection<User> users;
        private IMongoCollection<Post> posts;

        private IGraphClient clientNeo4j;

        public Operations()
        {
            client = new MongoClient(ConnectionString);
            database = client.GetDatabase("Test");
            users = database.GetCollection<User>("Users");
            posts = database.GetCollection<Post>("posts");

            clientNeo4j = new GraphClient(new Uri("http://localhost:7474/"), "neo4j", "neo4jpwd");
            clientNeo4j.ConnectAsync().Wait();
        }

        // create new user (register)
        public User SignUp(string firstName, string lastName, string email, string password, List<string> interests)
        {
            //Mongo
            var existingUser = users.Find(u => u.Email == email).SingleOrDefault();
            if (existingUser != null)
            {
                throw new ArgumentException("User with this email already exists.\n");
            }

            var newUser = new User(firstName, lastName, email, password, interests);
            users.InsertOne(newUser);

            //Neo4j
            string name = firstName + " " + lastName;
            Person newPerson = new Person { Name = name, Email = email };

            clientNeo4j.Cypher
                .Create("(person:Person $newPerson)")
                .WithParam("newPerson", newPerson)
                .ExecuteWithoutResultsAsync().Wait();

            return newUser;
        }

        public User LogIn(string email, string password)
        {
            User user = users.Find(u => u.Email == email && u.Password == password).SingleOrDefault();

            if (user == null)
            {
                throw new ArgumentException("User not found or invalid email or password.\n");
            }
            return user;
        }

        public void DeleteUser(User user)
        {
            //Mongo
            if (user == null)
            {
                throw new ArgumentException("User not found or invalid email or password.\n");
            }

            var filter = Builders<User>.Filter.Eq(u => u.Id, user.Id);
            var res = users.DeleteOne(filter);

            if (res.DeletedCount == 0)
            {
                throw new ArgumentException("Unable to delete user.\n");
            }

            //Neo4j
            string name = CreateName(user);

            clientNeo4j.Cypher
                .Match("(p:Person {name: $personName})")
                .WithParam("personName", name)
                .DetachDelete("p")
                .ExecuteWithoutResultsAsync().Wait();
        }

        public User FindUserByFirstName(string firstName)
        {
            var user = users.Find(u => u.FirstName == firstName).SingleOrDefault();
            if (user == null)
            {
                throw new ArgumentException("User with this name does not exist.\n");
            }
            return user;
        }

        public User FindUserByFullName(string firstName, string lastName)
        {
            var user = users.Find(u => u.FirstName == firstName && u.LastName == lastName).SingleOrDefault();
            if (user == null)
            {
                throw new ArgumentException("User with this name does not exist.\n");
            }
            return user;
        }

        public User FindUserById(string userId)
        {
            var user = users.Find(u => u.Id == userId).SingleOrDefault();
            if (user == null)
            {
                throw new ArgumentException("User with this ID does not exist.\n");
            }
            return user;
        }

        public Post FindPostById(string postId)
        {
            var post = posts.Find(p => p.Id == postId).SingleOrDefault();
            if (post == null)
            {
                throw new ArgumentException("Post with this ID does not exist.\n");
            }
            return post;
        }

        public List<Post> PostsOfFollowedUsers(User currentUser)
        {
            var followedUsersIds = currentUser.Following;

            var filter = Builders<Post>.Filter.In(u => u.UserId, followedUsersIds);
            var sortCondition = Builders<Post>.Sort.Descending(u => u.PostDate);

            var postsOfFollowedUsers = posts.Find(filter).Sort(sortCondition).ToList();

            if (postsOfFollowedUsers.Count > 0)
            {
                return postsOfFollowedUsers;
            }
            else { throw new ArgumentException("\nYou are not following anyone or the users you follow haven't posted anything yet.\n"); };
        }

        public List<Post> PostsOfAllUsers()
        {
            List<Post> allPosts = posts.AsQueryable().OrderByDescending(p => p.PostDate).ToList();
            return allPosts;
        }

        public List<Post> PostsOfUser(User user)
        {
            var postsOfUser = posts.Find(p => p.UserId == user.Id).ToList();
            return postsOfUser;
        }

        public void ShowProfile(User user)
        {
            if (user == null)
            {
                throw new ArgumentException("User with this name does not exist.\n");
            }

            Console.WriteLine($"\nName: {user.FirstName} {user.LastName}");
            Console.WriteLine($"E-mail: {user.Email}");

            Console.WriteLine("Interests:");
            foreach (var val in user.Interests)
            {

                Console.WriteLine($"\t{val}");
            }

            Console.WriteLine("Subscribers:");
            foreach (var val in user.Subscribers)
            {
                var sub = FindUserById(val);
                Console.WriteLine($"\t{sub.FirstName} {sub.LastName}");
            }

            Console.WriteLine("Following:");
            foreach (var val in user.Following)
            {
                var foll = FindUserById(val);
                Console.WriteLine($"\t{foll.FirstName} {foll.LastName}");
            }
        }

        public void ShowPosts(List<Post> posts, Operations socialNetwork)
        {
            foreach (var post in posts)
            {
                Console.WriteLine($"\nText: {post.Title}");

                var author = socialNetwork.FindUserById(post.UserId);
                Console.WriteLine($"Author: {author.FirstName} {author.LastName}");
                Console.WriteLine($"Post Id: {post.Id}");
                Console.WriteLine($"Date: {post.PostDate}");
                
                Console.WriteLine("Likes:");
                foreach (var likeId in post.Likes)
                {
                    var user = socialNetwork.FindUserById(likeId);
                    Console.WriteLine($" - {user.FirstName} {user.LastName}");
                }

                Console.WriteLine("Comments:");
                foreach (var comment in post.Comments)
                {
                    var commentAuthor = socialNetwork.FindUserById(comment.UserId);
                    Console.WriteLine($"   {commentAuthor.FirstName} {commentAuthor.LastName}: \"{comment.CommentText}\"");
                }

                Console.WriteLine('\n');
            }
        }

        public void Subscribe(User currentUser, User userToSubscribeTo)
        {
            //Mongo
            if (!userToSubscribeTo.Subscribers.Contains(currentUser.Id))
            {
                var userToSubscribeFilter = Builders<User>.Filter.Eq(u => u.Id, userToSubscribeTo.Id);
                var userToSubscribeUpdate = Builders<User>.Update.Push(u => u.Subscribers, currentUser.Id);
                users.UpdateOne(userToSubscribeFilter, userToSubscribeUpdate);
            }

            if (!currentUser.Following.Contains(userToSubscribeTo.Id))
            {
                var currentUserFilter = Builders<User>.Filter.Eq(u => u.Id, currentUser.Id);
                var currentUserUpdate = Builders<User>.Update.Push(u => u.Following, userToSubscribeTo.Id);
                users.UpdateOne(currentUserFilter, currentUserUpdate);
            }

            //Neo4j
            string currentUserName = CreateName(currentUser);
            string userToSubscribeToName = CreateName(userToSubscribeTo);

            var youFollow = clientNeo4j.Cypher
                .Match("(p:Person {name: $personName})")
                .WithParam("personName", currentUserName)
                .OptionalMatch("(p)-[:FOLLOWS]->(p2:Person {name: $personName2})")
                .WithParam("personName2", userToSubscribeToName)
                .ReturnDistinct(r => Return.As<int>("CASE WHEN p2 IS NULL THEN 0 ELSE 1 END"))
                .ResultsAsync.Result;

            bool youFollowBool = youFollow.First() == 1;

            if (!youFollowBool)
            {
                clientNeo4j.Cypher
                .Match("(p:Person {name: $personName})", "(p2:Person {name: $personName2})")
                .WithParam("personName", currentUserName)
                .WithParam("personName2", userToSubscribeToName)
                .Create("(p)-[:FOLLOWS]->(p2)")
                .ExecuteWithoutResultsAsync().Wait();
            }
            
        }

        public void Unsubscribe(User currentUser, User userToUnsubscribeFrom)
        {
            //Mongo
            if (userToUnsubscribeFrom.Subscribers.Contains(currentUser.Id))
            {
                var userToUnsubscribeFilter = Builders<User>.Filter.Eq(u => u.Id, userToUnsubscribeFrom.Id);
                var userToUnsubscribeUpdate = Builders<User>.Update.Pull(u => u.Subscribers, currentUser.Id);

                users.UpdateOne(userToUnsubscribeFilter, userToUnsubscribeUpdate);
            }

            if (currentUser.Following.Contains(userToUnsubscribeFrom.Id))
            {
                var currentUserFilter = Builders<User>.Filter.Eq(u => u.Id, currentUser.Id);
                var currentUserUpdate = Builders<User>.Update.Pull(u => u.Following, userToUnsubscribeFrom.Id);

                users.UpdateOne(currentUserFilter, currentUserUpdate);
            }

            //Neo4j
            string currentUserName = CreateName(currentUser);
            string userToUnsubscribeFromName = CreateName(userToUnsubscribeFrom);

            var youFollow = clientNeo4j.Cypher
                .Match("(p:Person {name: $personName})")
                .WithParam("personName", currentUserName)
                .OptionalMatch("(p)-[:FOLLOWS]->(p2:Person {name: $personName2})")
                .WithParam("personName2", userToUnsubscribeFromName)
                .ReturnDistinct(r => Return.As<int>("CASE WHEN p2 IS NULL THEN 0 ELSE 1 END"))
                .ResultsAsync.Result;

            bool youFollowBool = youFollow.First() == 1;

            if (!youFollowBool)
            {
                clientNeo4j.Cypher
                .Match("(p:Person {name: $personName})-[r:FOLLOWS]->(p2:Person {name: $personName2})")
                .WithParam("personName", currentUserName)
                .WithParam("personName2", userToUnsubscribeFromName)
                .Delete("r")
                .ExecuteWithoutResultsAsync().Wait();
            }
        }

        public void LikePost(User currentUser, string postId)
        {
            var filter = Builders<Post>.Filter.Eq(p => p.Id, postId);
            var post = posts.Find(filter).SingleOrDefault();

            if (post != null)
            {
                if (!post.Likes.Contains(currentUser.Id))
                {
                    var update = Builders<Post>.Update.Push(p => p.Likes, currentUser.Id);
                    posts.UpdateOne(filter, update);
                }
                else { throw new ArgumentException("You have already liked this post.\n"); };
            }
            else { throw new ArgumentException("Post not found. Please try again.\n"); }

        }

        public void UnlikePost(User currentUser, string postId)
        {
            var filter = Builders<Post>.Filter.Eq(p => p.Id, postId);
            var post = posts.Find(filter).SingleOrDefault();

            if (post != null)
            {
                if (post.Likes.Contains(currentUser.Id))
                {
                    var update = Builders<Post>.Update.Pull(p => p.Likes, currentUser.Id);
                    posts.UpdateOne(filter, update);
                }
                else { throw new ArgumentException("You have not liked this post. Unable to remove a like.\n"); };
            }
            else { throw new ArgumentException("Post not found. Please try again.\n"); };
        }

        public void WritePost(User currentUser, string title)
        {
            Post newPost = new Post
            {
                Title = title,
                UserId = currentUser.Id,
                PostDate = DateTime.Now.ToString(),
                Likes = new List<string>(),
                Comments = new List<Comment>()
            };

            posts.InsertOne(newPost);
        }

        public void WriteComment(User currentUser, string commentText, Post post)
        {
            Comment newComment = new Comment
            {
                UserId = currentUser.Id,
                CommentText = commentText
            };

            var filter = Builders<Post>.Filter.Eq(p => p.Id, post.Id);
            var existingPost = posts.Find(filter).SingleOrDefault();  // or FindPostById(post.Id);

            if (existingPost == null)
            {
                throw new ArgumentException("Post not found. Unable to add a comment.\n");
            }

            existingPost.Comments.Add(newComment);
            posts.UpdateOne(filter, Builders<Post>.Update.Set(p => p.Comments, existingPost.Comments));
        }



        //Neo4j

        public string CreateName(User user)
        {
            return user.FirstName + " " + user.LastName;
        }
        public void ShowPerson(IEnumerable<Person> results)
        {
            if (results != null)
            {
                Console.WriteLine("Search Results:");
                foreach (var person in results)
                {
                    Console.WriteLine($"Name: {person.Name}, Email: {person.Email}");
                }
            }
            else
            {
                Console.WriteLine("No results found.");
            }
        }

        public void ShowRelationship(User currentUser, User targetUser)
        {
            if (currentUser == null || targetUser == null)
            {
                throw new ArgumentException("User not found or invalid email or password.\n");
            }

            string currentUserName = CreateName(currentUser);
            string targetUserName = CreateName(targetUser);

            if (currentUserName == targetUserName)
            {
                Console.WriteLine("\nThis is your profile.");
                return;
            }

            var findRelationship = clientNeo4j.Cypher
                .Match("(p:Person {name: $personName})")
                .WithParam("personName", currentUserName)
                .OptionalMatch("(p)-[:FOLLOWS]-(p2:Person {name: $personName2})")
                .WithParam("personName2", targetUserName)
                .ReturnDistinct(r => Return.As<int>("CASE WHEN p2 IS NULL THEN 0 ELSE 1 END"))
                .ResultsAsync.Result;

            var isSubscribed = clientNeo4j.Cypher
                .Match("(p:Person {name: $personName})")
                .WithParam("personName", currentUserName)
                .OptionalMatch("(p)<-[:FOLLOWS]-(p2:Person {name: $personName2})")
                .WithParam("personName2", targetUserName)
                .ReturnDistinct(r => Return.As<int>("CASE WHEN p2 IS NULL THEN 0 ELSE 1 END"))
                .ResultsAsync.Result;

            bool relationshipExists = findRelationship.First() == 1;
            bool isSubscribedBool = isSubscribed.First() == 1;

            if (relationshipExists)
            {
                Console.Write("\nRelationship: ");
                if (isSubscribedBool) { Console.WriteLine("This person follows you."); }
                else { Console.WriteLine("You follow this person."); }
            }
            else
            {
                Console.WriteLine("\nYou don't have any relationship with this person.");
            }
        }

        public void ShowShortestPath(User currentUser, User targetUser)
        {
            if (currentUser == null || targetUser == null)
            {
                throw new ArgumentException("User not found or invalid email or password.\n");
            }

            string currentUserName = CreateName(currentUser);
            string targetUserName = CreateName(targetUser);

            if (currentUserName == targetUserName)
            {
                Console.WriteLine("\nShortest path: 0.");
                return;
            }

            var shortestPathQuery = clientNeo4j.Cypher
                .Match("p=shortestPath((curr:Person {name: $startName})-[*]-(target:Person {name: $endName}))")
                .WithParam("startName", currentUserName)
                .WithParam("endName", targetUserName)
                .Return(p => Return.As<IEnumerable<string>>("nodes(p)"))
                .ResultsAsync.Result;

            var nodesInPath = shortestPathQuery.First();
            var fullNames = nodesInPath.Select(nodeName => nodeName.Split('"')[3]);

            string concatenatedNames = string.Join(" - ", fullNames);
            Console.WriteLine($"Shortest path:\n {concatenatedNames}");
        }
    }
}
