﻿using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public Operations()
        {
            client = new MongoClient(ConnectionString);
            database = client.GetDatabase("Test");
            users = database.GetCollection<User>("Users");
            posts = database.GetCollection<Post>("posts");
        }

        public User SignUp(string firstName, string lastName, string email, string password, List<string> interests)
        {
            var existingUser = users.Find(u => u.Email == email).SingleOrDefault();
            if (existingUser != null)
            {
                throw new ArgumentException("User with this email already exists.\n");
            }

            var newUser = new User(firstName, lastName, email, password, interests);


            users.InsertOne(newUser);
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
        }

        public void Unsubscribe(User currentUser, User userToUnsubscribeFrom)
        {
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
    }
}
