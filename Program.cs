using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Neo4jClient;
using Neo4jClient.Cypher;
using Neo4j.Driver;
using Newtonsoft.Json;
using MongoDB.Driver;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Xml.Linq;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;
using Amazon.Auth.AccessControlPolicy;
using System.Collections;

namespace MongoDBConsoleApp
{

    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("firstName")]
        public string FirstName { get; set; }

        [BsonElement("lastName")]
        public string LastName { get; set; }

        [BsonElement("email")]
        public string Email { get; set; }

        [BsonElement("password")]
        public string Password { get; set; }

        [BsonElement("interests")]
        public List<string> Interests { get; set; }

        [BsonElement("subscribers")]
        public List<string> Subscribers { get; set; }

        [BsonElement("following")]
        public List<string> Following { get; set; }

        public User(string firstName, string lastName, string email, string password, List<string> interests = null)
        {
            this.FirstName = firstName;
            this.LastName = lastName;
            this.Email = email;
            this.Password = password;
            this.Interests = interests ?? new List<string> { "interest1" };
            this.Subscribers = new List<string>();
            this.Following = new List<string>();
        }
    }

    public class Post
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("title")]
        public string Title { get; set; }

        [BsonElement("author")]
        public string UserId { get; set; }

        [BsonElement("date")]
        public string PostDate { get; set; }

        [BsonElement("comments")]
        public List<Comment> Comments { get; set; }

        [BsonElement("likes")]
        public List<string> Likes { get; set; }
    }

    public class Comment
    {
        [BsonElement("author")]
        public string UserId { get; set; }

        [BsonElement("text")]
        public string CommentText { get; set; }
    }


    public class Person
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "email")]
        public string Email { get; set; }

    }

    class Program
    {
        private string[] interests = new string[]{

            "reading", "writing", "dancing", "drawing",
            "boxing", "hiking", "archery", "exercising",
            "programming", "video editing", "tennis", "badminton",
            "filmmaking", "sewing", "gaming", "cooking",
            "travelling",
        };

        static void Main(string[] args)
        {
            
            var socialNetwork = new Operations();
            User currentUser = null;
            Console.WriteLine("-------------- Social Network --------------");
            string loginMessage = "\nLog in or sign up to access this feature.";

            while (true)
            {
                Console.WriteLine("\n0. Sign up (register)");
                Console.WriteLine("1. Log in");
                Console.WriteLine("2. Display feed");
                Console.WriteLine("3. Find a user by name");
                Console.WriteLine("4. Subscribe to a user");
                Console.WriteLine("5. Unsubscribe from a user");
                Console.WriteLine("6. Find all posts of a user");
                Console.WriteLine("7. Like a post");
                Console.WriteLine("8. Remove like from a post");
                Console.WriteLine("9. Comment a post");
                Console.WriteLine("10. Write a post");
                Console.WriteLine("11. Log out");
                Console.WriteLine("12. Delete your profile");
                Console.WriteLine("13. Exit\n");

                Console.Write("Select an option: ");

                string userChoice = Console.ReadLine();
                switch (userChoice)
                {
                    case "0":
                        {
                            Console.Write("Enter your name: ");
                            string firstName = Console.ReadLine();

                            Console.Write("Enter your surname: ");
                            string lastName = Console.ReadLine();

                            Console.Write("Enter your email: ");
                            string email = Console.ReadLine();

                            Console.Write("Enter your password: ");
                            string password = Console.ReadLine();

                            List<string> interests = new List<string>();
                            string input;

                            Console.WriteLine("Enter your interests (one per line). Type '.' to finish:");

                            while (true)
                            {
                                input = Console.ReadLine();
                                if (input == ".")
                                    break;

                                interests.Add(input);
                            }
                            try
                            {
                                currentUser = socialNetwork.SignUp(firstName, lastName, email, password, interests);
                                Console.WriteLine("\nYou signed up successfully.");
                                List<Post> allPosts = socialNetwork.PostsOfAllUsers();
                                socialNetwork.ShowPosts(allPosts, socialNetwork);
                            }
                            catch (Exception ex) { Console.WriteLine(ex.Message); }

                            break;
                        }
                    case "1":
                        {
                            Console.Write("Enter your email: ");
                            string email = Console.ReadLine();

                            Console.Write("Enter your password: ");
                            string password = Console.ReadLine();
                            try
                            {
                                currentUser = socialNetwork.LogIn(email, password);
                                Console.WriteLine($"\nLogged in as {currentUser.FirstName} {currentUser.LastName}\n");
                                socialNetwork.ShowProfile(currentUser);

                                Console.WriteLine("\nPosts of users you are following:\n");
                                List<Post> posts = socialNetwork.PostsOfFollowedUsers(currentUser);
                                socialNetwork.ShowPosts(posts, socialNetwork);
                            }
                            catch (Exception ex) { Console.WriteLine(ex.Message); }

                            break;
                        }
                    case "2":
                        {
                            if (currentUser != null)
                            {
                                try
                                {
                                    var followedUsersPosts = socialNetwork.PostsOfFollowedUsers(currentUser);
                                    Console.WriteLine("\nPosts of users you are following:\n");
                                    socialNetwork.ShowPosts(followedUsersPosts, socialNetwork);
                                }
                                catch (Exception ex) { Console.WriteLine(ex.Message); }
                            }
                            else { Console.WriteLine(loginMessage); }
                            break;
                        }
                    case "3":
                        {
                            if (currentUser != null)
                            {
                                Console.Write("Enter the name of the user you want to find: ");
                                string firstName = Console.ReadLine();
                                Console.Write("Enter the surname of the user you want to find: ");
                                string lastName = Console.ReadLine();

                                try
                                {
                                    User targetUser = socialNetwork.FindUserByFullName(firstName, lastName);
                                    socialNetwork.ShowProfile(targetUser);
                                    socialNetwork.ShowRelationship(currentUser, targetUser);
                                    socialNetwork.ShowShortestPath(currentUser, targetUser);
                                }
                                catch (Exception ex) { Console.WriteLine(ex.Message); }
                            }
                            else { Console.WriteLine(loginMessage); }
                            break;
                        }
                    case "4":
                        {
                            if (currentUser != null)
                            {
                                Console.Write("Enter the name of the user you want to subscribe to: ");
                                string firstName = Console.ReadLine();
                                Console.Write("Enter the surname of the user you want to subscribe to: ");
                                string lastName = Console.ReadLine();

                                try
                                {
                                    User userToSubscribe = socialNetwork.FindUserByFullName(firstName, lastName);
                                    socialNetwork.Subscribe(currentUser, userToSubscribe);

                                    Console.WriteLine($"\nYou are now following {userToSubscribe.FirstName} {userToSubscribe.LastName}.");

                                }
                                catch (Exception ex) { Console.WriteLine(ex.Message); }
                            }
                            else { Console.WriteLine(loginMessage); }
                            break;
                        }
                    case "5":
                        {
                            if (currentUser != null)
                            {
                                Console.Write("Enter the name of the user you want to unsubscribe from: ");
                                string firstName = Console.ReadLine();
                                Console.Write("Enter the surname of the user you want to unsubscribe from: ");
                                string lastName = Console.ReadLine();

                                try
                                {
                                    User userToUnsubscribe = socialNetwork.FindUserByFullName(firstName, lastName);
                                    //Console.WriteLine($"\nUser to unsubscribe from id: {userToUnsubscribe.Id}");
                                    socialNetwork.Unsubscribe(currentUser, userToUnsubscribe);

                                    Console.WriteLine($"\nYou are not following {userToUnsubscribe.FirstName} {userToUnsubscribe.LastName} anymore.");

                                }
                                catch (Exception ex) { Console.WriteLine(ex.Message); }
                            }
                            else { Console.WriteLine(loginMessage); }
                            break;
                        }

                    case "6":
                        {
                            if (currentUser != null)
                            {
                                Console.Write("Enter the name of the user whose posts you want to see: ");
                                string firstName = Console.ReadLine();
                                Console.Write("Enter the surname of the user whose posts you want to see: ");
                                string lastName = Console.ReadLine();

                                try
                                {
                                    User userToFindPosts = socialNetwork.FindUserByFullName(firstName, lastName);
                                    List<Post> posts = socialNetwork.PostsOfUser(userToFindPosts);
                                    socialNetwork.ShowPosts(posts, socialNetwork);
                                }
                                catch (Exception ex) { Console.WriteLine(ex.Message); }
                            }
                            else { Console.WriteLine(loginMessage); }
                            break;
                        }
                    case "7":
                        {
                            if (currentUser != null)
                            {
                                Console.Write("Enter the ID of a post you want to like: ");
                                string postId = Console.ReadLine();
                                try
                                {
                                    socialNetwork.LikePost(currentUser, postId);
                                    Console.WriteLine("\nYou liked the post.");
                                }
                                catch (Exception ex) { Console.WriteLine(ex.Message); }
                            }
                            else { Console.WriteLine(loginMessage); }
                            break;
                        }
                    case "8":
                        {
                            if (currentUser != null)
                            {
                                Console.Write("Enter the ID of a post you want to remove your like from: ");
                                string postId = Console.ReadLine();
                                try
                                {
                                    socialNetwork.UnlikePost(currentUser, postId);
                                    Console.WriteLine("\nYou removed your like from the post.");
                                }
                                catch (Exception ex) { Console.WriteLine(ex.Message); }
                            }
                            else { Console.WriteLine(loginMessage); }
                            break;
                        }
                    case "9":
                        {
                            if (currentUser != null)
                            {
                                Console.Write("Enter the ID of the post you want to comment: ");
                                string postId = Console.ReadLine();
                                Post post = socialNetwork.FindPostById(postId);

                                Console.Write("Enter the text of the comment: ");
                                string comment = Console.ReadLine();

                                try
                                {
                                    socialNetwork.WriteComment(currentUser, comment, post);
                                    Console.WriteLine("\nComment created successfully.");
                                }
                                catch (Exception ex) { Console.WriteLine(ex.Message); }
                            }
                            else { Console.WriteLine(loginMessage); }
                            break;
                        }
                    case "10":
                        {
                            if (currentUser != null)
                            {
                                Console.Write("Enter the text of the post: ");
                                string postTitle = Console.ReadLine();
                                socialNetwork.WritePost(currentUser, postTitle);
                                Console.WriteLine("\nPost created successfully.\n");
                            }
                            else { Console.WriteLine(loginMessage); }
                            break;
                        }
                    case "11":
                        {
                            if (currentUser != null)
                            {
                                currentUser = null;
                                Console.WriteLine("You have logged out successfully.");
                            }
                            else { Console.WriteLine(loginMessage); }
                            break;
                        }
                    case "12":
                        {
                            if (currentUser != null)
                            {
                                socialNetwork.DeleteUser(currentUser);
                                currentUser = null;
                                Console.WriteLine("You have successfully deleted your profile.");
                            }
                            else { Console.WriteLine(loginMessage); }
                            break;
                        }
                    case "13":
                        Console.WriteLine("You have logged out successfully.");
                        return;

                        break;

                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }
            }
        }
    }
}