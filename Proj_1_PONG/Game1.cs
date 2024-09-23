using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace last_pong;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    private RenderTarget2D _doubleBuffer;
    private Rectangle _renderRectangle;
    private Texture2D _texture;
    private Random rand = new Random();

    //game sounds
    SoundEffect pad1;
    SoundEffect pad2;
    SoundEffect wall;
  

    //game variables
    public Rectangle[] paddles;
    public Color[] paddle_colors;
    public int[] color_selector = {5,1};//3 bits...

    public int[] limit = {20,100};
    public int[] heights = {32,16};
    private Rectangle _ball;
    private Point _ball_velocity;
    private bool _last_point_side = false;
    private readonly Random _rand;

    //dumb paddle stuff
    private int paddle_acc = 0;
    private int paddel_vel = 0;
    private int[] scores = {0,0};

    //gamestate items
    public enum GameState {Idle, Start, Play, CheckEnd}
    private GameState _game_state;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        this.TargetElapsedTime = new TimeSpan(333333);
        Window.AllowUserResizing = true;

        _rand = new Random();
    }

    protected override void Initialize()
    {
        _doubleBuffer = new RenderTarget2D(GraphicsDevice,640,480);

        _graphics.PreferredBackBufferWidth = 1440;
        _graphics.PreferredBackBufferHeight = 720;

        //_graphics.IsFullScreen = true;

        _graphics.ApplyChanges();

        Window.ClientSizeChanged += window_change;
        window_change(null,null);

        reset_ball();

        //paddle creation
        paddles = new Rectangle[]{
            new Rectangle(32,_doubleBuffer.Height/2-16,8,heights[0]),
            new Rectangle(_doubleBuffer.Width-40, _doubleBuffer.Height/2-16,8,heights[1])};
                
        paddle_colors = new Color[]{//third color is ball
            new Color(255,255,255,255),
            new Color(50,150,255,255),
            new Color(255,255,255,255)};
        //last statement
        base.Initialize();
    }

    private void window_change(object sender, EventArgs e)
    {
        var width = Window.ClientBounds.Width;
        var height = Window.ClientBounds.Height;

        if(height < width / (float)_doubleBuffer.Width * _doubleBuffer.Height)
        {
            width = (int)(height / (float)_doubleBuffer.Height * _doubleBuffer.Width);
        }
        else
        {
            height = (int)(width/(float)_doubleBuffer.Width*_doubleBuffer.Height);
        }

        var x = (Window.ClientBounds.Width - width) / 2;
        var y = (Window.ClientBounds.Height- height) / 2;

        _renderRectangle = new Rectangle(x, y, width, height);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Creating the 1 current texture for pong
        _texture = new Texture2D(GraphicsDevice, 1, 1);
        Color[] data = new Color[1];
        data[0] = Color.White;
        _texture.SetData(data);

        //sound loading
        pad1 = Content.Load<SoundEffect>("Sounds/Sound1");
        pad2 = Content.Load<SoundEffect>("Sounds/Sound2");
        wall = Content.Load<SoundEffect>("Sounds/Sound3");
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        // TODO: Add your update logic here
        switch(_game_state)
        {
            case GameState.Idle:
                move_ball(true);
                _game_state = GameState.Start;
                break;
            case GameState.Start:
                reset_ball();
            
                _game_state = GameState.Play;
                break;
            case GameState.Play:
                var score = move_ball(false);

                //paddle section
                player_paddle(0);
                AI_paddle(1);
                paddle_ball_check();
                rule_check();

                if(score == 1)//left scored on right
                {
                    _last_point_side = true;
                    _game_state = GameState.CheckEnd;
                    scores[1]++;
                }
                if(score == -1)//right scored on left
                {
                    _last_point_side = false;
                    _game_state = GameState.CheckEnd;
                    scores[0]++;
                }
                break;
            case GameState.CheckEnd:
                reset_ball();
                _game_state = GameState.Play;
                break;
            default:
                _game_state = GameState.Idle;
                break;
        }

        base.Update(gameTime);
    }

    // draw game here
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.SetRenderTarget(_doubleBuffer);
        GraphicsDevice.Clear(Color.Black);

        // court
        _spriteBatch.Begin();
        for(int i = 0; i < 31; i++)
        {
            _spriteBatch.Draw(
                _texture,
                new Rectangle(_doubleBuffer.Width/2, i * _doubleBuffer.Height/31,
                2,      _doubleBuffer.Height/62),
                Color.White);
        }
        _spriteBatch.End();

        //Ball states
        _spriteBatch.Begin();
        switch(_game_state)
        {
            case GameState.Idle:
                _spriteBatch.Draw(_texture, _ball, paddle_colors[2]);
                break;
            case GameState.Start:
                break;
            case GameState.Play:
                _spriteBatch.Draw(_texture, _ball, paddle_colors[2]);

                _spriteBatch.Draw(_texture, paddles[0], paddle_colors[0]);
                _spriteBatch.Draw(_texture, paddles[1], paddle_colors[1]);
                break;
            case GameState.CheckEnd:
                _spriteBatch.Draw(_texture, _ball, paddle_colors[2]);

                _spriteBatch.Draw(_texture, paddles[0], paddle_colors[0]);
                _spriteBatch.Draw(_texture, paddles[1], paddle_colors[1]);
                break;
        }
        _spriteBatch.End();

        //Background
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.CornflowerBlue);
        _spriteBatch.Begin();
        _spriteBatch.Draw(_doubleBuffer, _renderRectangle, Color.White);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

#region functions
    private void reset_ball()
    {
        _ball = new Rectangle(_doubleBuffer.Width/2 - 4, _doubleBuffer.Height/2-4,8,8);
        _ball_velocity = new Point(
            _last_point_side ? _rand.Next(2,7) : -_rand.Next(2,7),
            _rand.Next() > int.MaxValue/2 ? _rand.Next(2,7) : -_rand.Next(2,7)
        );
    }

    private int move_ball(bool bounce_off_sides)
    {
        _ball.X += _ball_velocity.X;
        _ball.Y += _ball_velocity.Y;

        //Y
        if(_ball.Y < 0)
        {
            _ball.Y = -_ball.Y;
            _ball_velocity.Y = -_ball_velocity.Y; 
            wall.Play();
        }
        if(_ball.Y + _ball.Height > _doubleBuffer.Height)
        {
            _ball.Y = _doubleBuffer.Height - _ball.Height - (_ball.Y + _ball.Height - _doubleBuffer.Height);
            _ball_velocity.Y = -_ball_velocity.Y;
            wall.Play();
        }

        //X
        if(_ball.X < 0)
        {
            if(bounce_off_sides)
            {
                _ball.X = 0;
                _ball_velocity.X = -_ball_velocity.X;
            }
            else return -1;
        }
        if(_ball.X + _ball.Width > _doubleBuffer.Width)
        {
            if(bounce_off_sides)
            {
                _ball.X = _doubleBuffer.Width - _ball.Width;
                _ball_velocity.X = -_ball_velocity.X;
            }
            else return -1;
        }

        return 0;
    }

    private void AI_paddle(int index)
    {
        var delta = _ball.Y + _ball.Height/2 - (paddles[index].Y + paddles[index].Height/2);
        
        int speed = 3;
        if(Math.Abs(delta) > speed)//speed limiter
        {
            delta = speed*Math.Sign(delta);
        }//*/

        paddles[index].Y += delta;
    }

    private void player_paddle(int index)
    {
        //var delta = 0;

        //inputs
            //UP KEY: Move AI
        if(Keyboard.GetState().IsKeyDown(Keys.Up) && paddles[index].Y >= 0)// UP
        {
            AI_paddle(index);
        }
            //DOWN KEY: Paddle size
        if(Keyboard.GetState().IsKeyDown(Keys.Down) && paddles[index].Y <= _doubleBuffer.Height - paddles[index].Height)// UP
        {
            heights[index] = Math.Min(limit[1], heights[index] + 4);
            if(heights[index] != limit[1]) paddles[index].Y +=1;
        }
        else//shrink paddle
        {
            heights[index] = Math.Max(limit[0], heights[index] - 2);
            if(heights[index] != limit[0]) paddles[index].Y -=1;
        }

        int num = heights[index]*2 + 120;
        paddle_colors[index] = color_toggle(num, 0);
        paddles[index].Height = heights[index];
    }

    private Color color_toggle(int num, int pad)
    {
        Color temp_color = new Color(50,50,50);
        if(num >= 255) num = 255;

        int hold = color_selector[pad];
        for(int i = 2; i >= 0; i--)
        {
            int _temp = hold / (int)Math.Pow(2,i);

            if(hold >= (int)Math.Pow(2,i)) hold -= (int)Math.Pow(2,i);
            
            if(_temp != 0)
            {
                switch(i)
                {
                    case 0:
                        temp_color.R = (byte)num;
                        break;
                    case 1:
                        temp_color.G = (byte)num;
                        break;
                    case 2:
                        temp_color.B = (byte)num;
                        break;
                }
            }
        }


        return temp_color;
    }

    private bool paddle_check(int index, int x, int y)
    {
        return  x                 <= paddles[index].X + paddles[index].Width &&
                x + _ball.Width   >= paddles[index].X &&
                y                 <= paddles[index].Y + paddles[index].Height &&
                y + _ball.Height  >= paddles[index].Y;
    }

    private void rule_check()
    {
        for (int i = 0; i < 2; i++)
        {
            if(paddles[i].Y <= 0)
            {
                paddles[i].Y = 2;
            }
        }
    }

    private bool paddle_ball_check()
    {
        float delta = 0;
        int pad = 0;

        if(_ball_velocity.X > 0 && _ball.X + _ball.Width > paddles[1].X)
        {
            delta = _ball.X + _ball.Width - paddles[1].X;
            if(delta > _ball_velocity.X + _ball.Width) return false;
            pad = 1;
        }
        else if(_ball_velocity.X < 0 && _ball.X < paddles[0].X + paddles[0].Width)
        {
            delta = _ball.X - (paddles[0].X + paddles[0].Width);
            if(delta < _ball_velocity.X) return false;
            pad = 0;
        }
        else return false;//final failsafe

        float deltaTime = delta / _ball_velocity.X;
        int coll_y = (int)(_ball.Y - _ball_velocity.Y * deltaTime);
        int coll_x = (int)(_ball.X - _ball_velocity.X * deltaTime);

        //check for the collision
        if(paddle_check(delta < 0 ? 0 : 1, coll_x, coll_y))
        {
            _ball.X = coll_x;
            _ball.Y = coll_y;

            color_selector[pad] = rand.Next(0,7);

            //sound section 
            if(pad == 0) 
            {
                pad1.Play(); 
                
            }
            else    
            {
                pad2.Play();
                paddle_colors[pad] = color_toggle(255, pad);
            }

            paddle_colors[2] = paddle_colors[pad];

            _ball_velocity.X = -(_ball_velocity.X + Math.Sign(_ball_velocity.X));
            
            var diff_Y = (coll_y + _ball.Height/2) - (paddles[pad].Y + paddles[pad].Height/2); 

            diff_Y /= paddles[pad].Height/8;
            diff_Y -= Math.Sign(diff_Y);

            _ball_velocity.Y += diff_Y;
            return true;
        }
        return false;
    }
#endregion functions
}
