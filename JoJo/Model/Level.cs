#region File Description
//-----------------------------------------------------------------------------
// Level.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;

using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

using System.IO;

using JoJo.Controller;
using JoJo.View;
using JoJo.Model;

namespace JoJo.Model
{
    /// <summary>
    /// A uniform grid of tiles with collections of gems and enemies.
    /// The level owns the player and controls the game's win and lose
    /// conditions as well as scoring.
    /// </summary>
    class Level : IDisposable
    {
      

        private GraphicsDevice graphics;

        // Physical structure of the level.
        private Tile[,] tiles;
        private Texture2D[] layers;
        // The layer which entities are drawn on top of.
        private const int EntityLayer = 2;

        // Entities in the level.
        public Player Player
        {
            get { return player; }
        }
        Player player;

		Texture2D projectileTexture;
		List<Projectile> projectiles;


		Texture2D projectileTexture2;
		List<Projectile> projectiles2;

        public Player2 Player2
        {
            get { return player2; }
        }
        Player2 player2;

        private List<Gem> gems = new List<Gem>();
        private List<Enemy> enemies = new List<Enemy>();

        // Key locations in the level.        
        private Vector2 start;
        private Point exit = InvalidPosition;
        private static readonly Point InvalidPosition = new Point(-1, -1);

        // Level game state.
        private Random random = new Random(354668); // Arbitrary, but constant seed

        public int Score
        {
            get { return score; }
        }

		public int Score2
		{
			get { return score2; }
		}


      

        public int Player1Ammo
        {
            get { return player1Ammo; }
        }

		public void setPlayer1Ammo(int ammo1)
		{
            player1Ammo = ammo1;
		}

		public void setPlayer2Ammo(int ammo2)
		{
			player2Ammo = ammo2;
		}

        public int Player2Ammo
        {
            get { return player2Ammo; }
        }
        int score;
		int score2;
        int player1Ammo;
        int player2Ammo;

        public bool ReachedExit
        {
            get { return reachedExit; }
        }
        bool reachedExit;

        public TimeSpan TimeRemaining
        {
            get { return timeRemaining; }
        }
        TimeSpan timeRemaining;

        private const int PointsPerSecond = 5;

        // Level content.        
        public ContentManager Content
        {
            get { return content; }
        }
        ContentManager content;

        private SoundEffect exitReachedSound;

		#region Loading

		/// <summary>
		/// Constructs a new level.
		/// </summary>
		/// <param name="serviceProvider">
		/// The service provider that will be used to construct a ContentManager.
		/// </param>
		/// <param name="fileStream">
		/// A stream containing the tile data.
		/// </param>
		/// 
		


        public Level(IServiceProvider serviceProvider, Stream fileStream, int levelIndex, GraphicsDevice graphics)
        {


            // Create a new content manager to load content used just by this level.
            content = new ContentManager(serviceProvider, "Content");

            timeRemaining = TimeSpan.FromMinutes(2.0);
            projectiles = new List<Projectile>();
			projectileTexture = Content.Load<Texture2D>("Sprites/Gem");
			projectiles2 = new List<Projectile>();
			projectileTexture2 = Content.Load<Texture2D>("Sprites/Gem");

            LoadTiles(fileStream);



            this.graphics = graphics;
			

            // Load background layer textures. For now, all levels must
            // use the same backgrounds and only use the left-most part of them.
            layers = new Texture2D[3];
            for (int i = 0; i < layers.Length; ++i)
            {
                // Choose a random segment if each background layer for level variety.
                int segmentIndex = levelIndex;
                layers[i] = Content.Load<Texture2D>("Backgrounds/Layer" + i + "_" + segmentIndex);
            }

            // Load sounds.
            exitReachedSound = Content.Load<SoundEffect>("Sounds/ExitReached");
        }



        /// <summary>
        /// Iterates over every tile in the structure file and loads its
        /// appearance and behavior. This method also validates that the
        /// file is well-formed with a player start point, exit, etc.
        /// </summary>
        /// <param name="fileStream">
        /// A stream containing the tile data.
        /// </param>
        private void LoadTiles(Stream fileStream)
        {

           
            UpdateProjectiles();
			UpdateProjectiles2();
            // Load the level and ensure all of the lines are the same length.
            int width;
            List<string> lines = new List<string>();
            using (StreamReader reader = new StreamReader(fileStream))
            {
                string line = reader.ReadLine();
                width = line.Length;
                while (line != null)
                {
                    lines.Add(line);
                    if (line.Length != width)
                        throw new Exception(String.Format("The length of line {0} is different from all preceeding lines.", lines.Count));
                    line = reader.ReadLine();
                }
            }

            // Allocate the tile grid.
            tiles = new Tile[width, lines.Count];

            // Loop over every tile position,
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    // to load each tile.
                    char tileType = lines[y][x];
                    tiles[x, y] = LoadTile(tileType, x, y);
                }
            }

            // Verify that the level has a beginning and an end.
            if (Player == null)
                throw new NotSupportedException("A level must have a starting point.");
            if (exit == InvalidPosition)
                throw new NotSupportedException("A level must have an exit.");

        }

        /// <summary>
        /// Loads an individual tile's appearance and behavior.
        /// </summary>
        /// <param name="tileType">
        /// The character loaded from the structure file which
        /// indicates what should be loaded.
        /// </param>
        /// <param name="x">
        /// The X location of this tile in tile space.
        /// </param>
        /// <param name="y">
        /// The Y location of this tile in tile space.
        /// </param>
        /// <returns>The loaded tile.</returns>
        private Tile LoadTile(char tileType, int x, int y)
        {
            switch (tileType)
            {
                // Blank space
                case '.':
                    return new Tile(null, TileCollision.Passable);

                // Exit
                case 'X':
                    return LoadExitTile(x, y);

                // Gem
                case 'G':
                    return LoadGemTile(x, y);

                // Floating platform
                case '-':
                    return LoadTile("Platform", TileCollision.Platform);

                // Various enemies
                case 'A':
                    return LoadEnemyTile(x, y, "MonsterA");
                case 'B':
                    return LoadEnemyTile(x, y, "MonsterB");
                case 'C':
                    return LoadEnemyTile(x, y, "MonsterC");
                case 'D':
                    return LoadEnemyTile(x, y, "MonsterD");

                // Platform block
                case '~':
                    return LoadVarietyTile("BlockB", 2, TileCollision.Platform);

                // Passable block
                case ':':
                    return LoadVarietyTile("BlockB", 2, TileCollision.Passable);

                // Player 1 start point
                case '1':
                    return LoadStartTile(x, y);

                // Impassable block
                case '#':
                    return LoadVarietyTile("BlockA", 7, TileCollision.Impassable);

                // Unknown tile type character
                default:
                    throw new NotSupportedException(String.Format("Unsupported tile type character '{0}' at position {1}, {2}.", tileType, x, y));
            }
        }

        /// <summary>
        /// Creates a new tile. The other tile loading methods typically chain to this
        /// method after performing their special logic.
        /// </summary>
        /// <param name="name">
        /// Path to a tile texture relative to the Content/Tiles directory.
        /// </param>
        /// <param name="collision">
        /// The tile collision type for the new tile.
        /// </param>
        /// <returns>The new tile.</returns>
        private Tile LoadTile(string name, TileCollision collision)
        {
            return new Tile(Content.Load<Texture2D>("Tiles/" + name), collision);
        }



     


        private void AddProjectile(Vector2 position)
        {
			Projectile projectile = new Projectile();
			projectile.Initialize(graphics.Viewport, projectileTexture, position);
			

			if (projectiles.Count < player1Ammo)
			{
				projectiles.Add(projectile);

			}

        }

		private void AddProjectile2(Vector2 position)
		{
			Projectile projectile2 = new Projectile();
			projectile2.Initialize(graphics.Viewport, projectileTexture2, position);


			if (projectiles2.Count < player2Ammo)
			{
				projectiles2.Add(projectile2);

			}

		}


        
        private void UpdateProjectiles()
        {
            

            // Update the Projectiles
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                projectiles[i].Update(Player.IsFacingLeft);

                if (projectiles[i].Active == false)
                {
                    projectiles.RemoveAt(i);
                }
            }
        }




		private void UpdateProjectiles2()
		{
            

			// Update the Projectiles
			for (int i = projectiles2.Count - 1; i >= 0; i--)
			{
				projectiles2[i].Update(Player2.IsFacingLeft);

				if (projectiles2[i].Active == false)
				{
					projectiles2.RemoveAt(i);
				}
			}
		}


        /// <summary>
        /// Loads a tile with a random appearance.
        /// </summary>
        /// <param name="baseName">
        /// The content name prefix for this group of tile variations. Tile groups are
        /// name LikeThis0.png and LikeThis1.png and LikeThis2.png.
        /// </param>
        /// <param name="variationCount">
        /// The number of variations in this group.
        /// </param>
        private Tile LoadVarietyTile(string baseName, int variationCount, TileCollision collision)
        {
            int index = random.Next(variationCount);
            return LoadTile(baseName + index, collision);
        }


        /// <summary>
        /// Instantiates a player, puts him in the level, and remembers where to put him when he is resurrected.
        /// </summary>
        private Tile LoadStartTile(int x, int y)
        {
            if (Player != null)
                throw new NotSupportedException("A level may only have one starting point.");

            start = RectangleExtensions.GetBottomCenter(GetBounds(x, y));
            player = new Player(this, start);
            player2 = new Player2(this, start);

            return new Tile(null, TileCollision.Passable);
        }

        /// <summary>
        /// Remembers the location of the level's exit.
        /// </summary>
        private Tile LoadExitTile(int x, int y)
        {
            if (exit != InvalidPosition)
                throw new NotSupportedException("A level may only have one exit.");

            exit = GetBounds(x, y).Center;

            return LoadTile("Exit", TileCollision.Passable);
        }

        /// <summary>
        /// Instantiates an enemy and puts him in the level.
        /// </summary>
        private Tile LoadEnemyTile(int x, int y, string spriteSet)
        {
            Vector2 position = RectangleExtensions.GetBottomCenter(GetBounds(x, y));
            enemies.Add(new Enemy(this, position, spriteSet));

            return new Tile(null, TileCollision.Passable);
        }

        /// <summary>
        /// Instantiates a gem and puts it in the level.
        /// </summary>
        private Tile LoadGemTile(int x, int y)
        {
            Point position = GetBounds(x, y).Center;
            gems.Add(new Gem(this, new Vector2(position.X, position.Y)));

            return new Tile(null, TileCollision.Passable);
        }

        /// <summary>
        /// Unloads the level content.
        /// </summary>
        public void Dispose()
        {
            Content.Unload();
        }

        #endregion

        #region Bounds and collision

        /// <summary>
        /// Gets the collision mode of the tile at a particular location.
        /// This method handles tiles outside of the levels boundries by making it
        /// impossible to escape past the left or right edges, but allowing things
        /// to jump beyond the top of the level and fall off the bottom.
        /// </summary>
        public TileCollision GetCollision(int x, int y)
        {
            // Prevent escaping past the level ends.
            if (x < 0 || x >= Width)
                return TileCollision.Impassable;
            // Allow jumping past the level top and falling through the bottom.
            if (y < 0 || y >= Height)
                return TileCollision.Passable;

            return tiles[x, y].Collision;
        }

        /// <summary>
        /// Gets the bounding rectangle of a tile in world space.
        /// </summary>        
        public Rectangle GetBounds(int x, int y)
        {
            return new Rectangle(x * Tile.Width, y * Tile.Height, Tile.Width, Tile.Height);
        }

        /// <summary>
        /// Width of level measured in tiles.
        /// </summary>
        public int Width
        {
            get { return tiles.GetLength(0); }
        }

        /// <summary>
        /// Height of the level measured in tiles.
        /// </summary>
        public int Height
        {
            get { return tiles.GetLength(1); }
        }

        #endregion

        #region Update

        /// <summary>
        /// Updates all objects in the world, performs collision between them,
        /// and handles the time limit with scoring.
        /// </summary>
        public void Update(
            GameTime gameTime,
            KeyboardState keyboardState,
            KeyboardState previousKeyboardState,
            GamePadState gamePadState,
            GamePadState gamePadState2,
            DisplayOrientation orientation)
        {
            // Pause while the player is dead or time is expired.
            if (!Player.IsAlive || TimeRemaining == TimeSpan.Zero || !Player2.IsAlive)
            {
                // Still want to perform physics on the player.
                Player.ApplyPhysics(gameTime);
                Player2.ApplyPhysics(gameTime);
            }
            else if (ReachedExit)
            {
                // Animate the time being converted into points.
                int seconds = (int)Math.Round(gameTime.ElapsedGameTime.TotalSeconds * 100.0f);
                seconds = Math.Min(seconds, (int)Math.Ceiling(TimeRemaining.TotalSeconds));
                timeRemaining -= TimeSpan.FromSeconds(seconds);
                //score += seconds * PointsPerSecond;
            }
            else
            {
                timeRemaining -= gameTime.ElapsedGameTime;
                Player.Update(gameTime, keyboardState, gamePadState, orientation);
                Player2.Update(gameTime, keyboardState, gamePadState2, orientation);
                UpdateGems(gameTime);

                // Falling off the bottom of the level kills the player.
                if (Player.BoundingRectangle.Top >= Height * Tile.Height || Player2.BoundingRectangle.Top >= Height * Tile.Height)
                    OnPlayerKilled(null);
                UpdateCollision();
                UpdateProjectiles();
				UpdateProjectiles2();
                UpdateEnemies(gameTime);

                // The player has reached the exit if they are standing on the ground and
                // his bounding rectangle contains the center of the exit tile. They can only
                // exit when they have collected all of the gems.
                if (Player.IsAlive &&
                    Player.IsOnGround &&
                    Player.BoundingRectangle.Contains(exit))
                {
                    OnExitReached();
					score += 10;
                }

                if (Player2.IsAlive &&
                   Player2.IsOnGround &&
                   Player2.BoundingRectangle.Contains(exit))
                {
                    OnExitReached();
					score2 += 10;
                }
            }

            // Clamp the time remaining at zero.
            if (timeRemaining < TimeSpan.Zero)
                timeRemaining = TimeSpan.Zero;






            if (keyboardState.IsKeyDown(Keys.RightShift) && player1Ammo != 0 )
            {
                if(previousKeyboardState.IsKeyDown(Keys.RightShift) && timeRemaining.Milliseconds %2== 1&& timeRemaining.Milliseconds % 3 == 1)
                {
					AddProjectile(player.Position + new Vector2(player.Width / 2, 0));
					UpdateProjectiles();
					player1Ammo--;
                }
               
           
				
               
            }

            if (keyboardState.IsKeyDown(Keys.NumPad0) && player1Ammo != 0 )
            {
                
                AddProjectile(player.Position + new Vector2(player.Width/2,0));
                UpdateProjectiles();
                player1Ammo--;
            }






			if (keyboardState.IsKeyDown(Keys.LeftShift) && player2Ammo != 0)
			{
				if (previousKeyboardState.IsKeyDown(Keys.LeftShift) && timeRemaining.Milliseconds % 2 == 1 && timeRemaining.Milliseconds % 3 == 1)
				{
					AddProjectile2(player2.Position + new Vector2(player2.Width / 2, 0));
					UpdateProjectiles2();
					player2Ammo--;
				}




			}

            if (keyboardState.IsKeyDown(Keys.F) && player2Ammo != 0)
			{

				AddProjectile2(player2.Position + new Vector2(player2.Width / 2, 0));
				UpdateProjectiles2();
				player2Ammo--;
			}


        }

        /// <summary>
        /// Animates each gem and checks to allows the player to collect them.
        /// </summary>
        private void UpdateGems(GameTime gameTime)
        {
            for (int i = 0; i < gems.Count; ++i)
            {
                Gem gem = gems[i];

                gem.Update(gameTime);

                if (gem.BoundingCircle.Intersects(Player.BoundingRectangle))
                {
                    gems.RemoveAt(i--);
                    OnGemCollected(gem, Player);
                }

                if (gem.BoundingCircle.Intersects(Player2.BoundingRectangle))
                {
                    gems.RemoveAt(i--);
                    OnGemCollected2(gem, Player);
                }
            }
        }

        /// <summary>
        /// Animates each enemy and allow them to kill the player.
        /// </summary>
        private void UpdateEnemies(GameTime gameTime)
        {
            foreach (Enemy enemy in enemies)
            {
                enemy.Update(gameTime);

                // Touching an enemy instantly kills the player
                if (enemy.BoundingRectangle.Intersects(Player.BoundingRectangle))
                {
                    OnPlayerKilled(enemy);
                }

                if (enemy.BoundingRectangle.Intersects(Player2.BoundingRectangle))
                {
                    OnPlayerKilled(enemy);
                }
            }
        }


        /// <summary>
        /// Called when a gem is collected.
        /// </summary>
        /// <param name="gem">The gem that was collected.</param>
        /// <param name="collectedBy">The player who collected this gem.</param>
        private void OnGemCollected(Gem gem, Player collectedBy)
        {


            score += Gem.PointValue;


            if (collectedBy == player)
            {
                player1Ammo++;
            }

            else
            {
                player2Ammo++;
            }



            gem.OnCollected(collectedBy);
        }

        private void OnGemCollected2(Gem gem, Player collectedBy)
        {


         


            if (collectedBy == player)
            {
                player2Ammo++;
				score2 += Gem.PointValue;
            }

            else
            {
                player1Ammo++;
				score += Gem.PointValue;
            }

            gem.OnCollected(collectedBy);
        }


        /// <summary>
        /// Called when the player is killed.
        /// </summary>
        /// <param name="killedBy">
        /// The enemy who killed the player. This is null if the player was not killed by an
        /// enemy, such as when a player falls into a hole.
        /// </param>
        private void OnPlayerKilled(Enemy killedBy)
        {
            Player.OnKilled(killedBy);
            Player2.OnKilled(killedBy);
        }




        private void UpdateCollision()
        {
            // Use the Rectangle's built-in intersect function to 
            // determine if two objects are overlapping
            Rectangle rectangle1;
            Rectangle rectangle2;
            Rectangle player1ProjectileBox;
			Rectangle player2ProjectileBox;
          

            // Only create the rectangle once for the player
            rectangle1 = new Rectangle((int)player.Position.X,
            (int)player.Position.Y,
            player.Width,
            player.Height);

            rectangle2 = new Rectangle((int)player2.Position.X,
           (int)player2.Position.Y,
           player.Width,
           player.Height);

          

			for (int i = 0; i < projectiles.Count; i++)
			{

				{
					// Create the rectangles we need to determine if we collided with each other
					player1ProjectileBox = new Rectangle((int)projectiles[i].Position.X -
					projectiles[i].Width / 2, (int)projectiles[i].Position.Y -
					projectiles[i].Height / 2, projectiles[i].Width, projectiles[i].Height);


					// Determine if the two objects collided with each other
					if (rectangle2.Intersects(player1ProjectileBox))
					{
						
                        player2.damageAndKilled();
                        player2.playerKill();

                        score += 8;

						projectiles[i].Active = false;
					}
				}
			}







            for (int i = 0; i < projectiles2.Count; i++)
            {

                {
                    // Create the rectangles we need to determine if we collided with each other
                    player2ProjectileBox = new Rectangle((int)projectiles2[i].Position.X -
                    projectiles2[i].Width / 2, (int)projectiles2[i].Position.Y -
                    projectiles2[i].Height / 2, projectiles2[i].Width, projectiles2[i].Height);


                    // Determine if the two objects collided with each other
                    if (rectangle1.Intersects(player2ProjectileBox))
                    {
                        
                        player.damageAndKilled();
                        player.playerKill();

                        score += 8;

                        projectiles2[i].Active = false;
                    }
                }
            }

        }



        private void UpdateProjectile(GameTime gameTime)
        {

            for (int index = projectiles.Count - 1; index >= 0; index--)
            {
                if (!projectiles[index].Active)
                {

                    projectiles.RemoveAt(index);
                }
            }

        }



        private void UpdateProjectile2(GameTime gameTime)
        {

            for (int index = projectiles2.Count - 1; index >= 0; index--)
            {
                if (!projectiles2[index].Active)
                {

                    projectiles2.RemoveAt(index);
                }
            }

        }



        /// <summary>
        /// Called when the player reaches the level's exit.
        /// </summary>
        private void OnExitReached()
        {
            Player.OnReachedExit();
			 Player2.OnReachedExit();
            exitReachedSound.Play();
            reachedExit = true;
        }

        /// <summary>
        /// Restores the player to the starting point to try the level again.
        /// </summary>
        public void StartNewLife()
        {
            Player.Reset(start);
            Player.setHealth(1);
			 Player2.Reset(start);
            Player2.setHealth(1);
            setPlayer2Ammo(0);
            setPlayer1Ammo(0);
        }

        #endregion

        #region Draw

        /// <summary>
        /// Draw everything in the level from background to foreground.
        /// </summary>
        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            for (int i = 0; i <= EntityLayer; ++i)
                spriteBatch.Draw(layers[i], Vector2.Zero, Color.White);

            DrawTiles(spriteBatch);

            foreach (Gem gem in gems)
                gem.Draw(gameTime, spriteBatch);

            Player.Draw(gameTime, spriteBatch);
			 Player2.Draw(gameTime, spriteBatch);

            foreach (Enemy enemy in enemies)
                enemy.Draw(gameTime, spriteBatch);

            for (int i = EntityLayer + 1; i < layers.Length; ++i)
                spriteBatch.Draw(layers[i], Vector2.Zero, Color.White);

			for (int i = 0; i < projectiles.Count; i++)
			{
				projectiles[i].Draw(spriteBatch);
			}


            for (int i = 0; i < projectiles2.Count; i++)
            {
                projectiles2[i].Draw(spriteBatch);
            }
        }

        /// <summary>
        /// Draws each tile in the level.
        /// </summary>
        private void DrawTiles(SpriteBatch spriteBatch)
        {
            // For each tile position
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    // If there is a visible tile in that position
                    Texture2D texture = tiles[x, y].Texture;
                    if (texture != null)
                    {
                        // Draw it in screen space.
                        Vector2 position = new Vector2(x, y) * Tile.Size;
                        spriteBatch.Draw(texture, position, Color.White);
                    }
                }
            }
        }

        #endregion
    }
}
