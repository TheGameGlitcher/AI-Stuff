using Raylib_cs;
using System;
using System.Numerics;
using System.Collections.Generic;

namespace AI_Agent_Test_2;

class Program
{
    struct Obstacle
    {
        public Vector3 Min;
        public Vector3 Max;
        public Color Color;

        public Obstacle(Vector3 min, Vector3 max, Color color)
        {
            Min = min;
            Max = max;
            Color = color;
        }
    }

    static void Main()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        Raylib.InitWindow(screenWidth, screenHeight, "3D Program - Perimeter Fence & Collision");
        Raylib.SetTargetFPS(60);
        Raylib.DisableCursor(); // Capture mouse cursor

        // Camera Setup
        Camera3D camera = new Camera3D
        {
            Position = new Vector3(10.0f, 10.0f, 10.0f),
            Target = new Vector3(0.0f, 0.0f, 0.0f),
            Up = new Vector3(0.0f, 1.0f, 0.0f),
            FovY = 45.0f,
            Projection = CameraProjection.Perspective
        };

        // Model Setup
        Mesh cubeMesh = Raylib.GenMeshCube(2.0f, 2.0f, 2.0f);
        Model cubeModel = Raylib.LoadModelFromMesh(cubeMesh);

        Mesh planeMesh = Raylib.GenMeshPlane(30.0f, 30.0f, 1, 1);
        Model groundModel = Raylib.LoadModelFromMesh(planeMesh);

        // Player Character (a Sphere)
        Mesh sphereMesh = Raylib.GenMeshSphere(1.0f, 16, 16);
        Model playerModel = Raylib.LoadModelFromMesh(sphereMesh);

        float rotationAngle = 0.0f;
        float cameraRotationX = 0.0f;
        float cameraRotationY = 0.0f;
        float moveSpeed = 0.15f;

        // Physics variables
        Vector3 playerPos = new Vector3(0, 2.0f, 0);
        Vector3 playerVel = new Vector3(0, 0, 0);
        float gravity = -9.81f;
        const float groundLevel = 0.0f;
        
        // Platform Bounds
        const float platformHalfSize = 15.0f; // Total size 30x30

        // Obstacle List (Pillars and Boards)
        List<Obstacle> obstacles = new List<Obstacle>();

        // Generate Pillars at corners and midpoints
        Vector3[] pillarPositions = {
            new Vector3(platformHalfSize, 2.5f, platformHalfSize),
            new Vector3(-platformHalfSize, 2.5f, platformHalfSize),
            new Vector3(platformHalfSize, 2.5f, -platformHalfSize),
            new Vector3(-platformHalfSize, 2.5f, -platformHalfSize),
            new Vector3(0, 2.5f, platformHalfSize),
            new Vector3(0, 2.5f, -platformHalfSize),
            new Vector3(platformHalfSize, 2.5f, 0),
            new Vector3(-platformHalfSize, 2.5f, 0)
        };

        foreach (var pos in pillarPositions)
        {
            obstacles.Add(new Obstacle(
                new Vector3(pos.X - 1.0f, pos.Y - 2.5f, pos.Z - 1.0f),
                new Vector3(pos.X + 1.0f, pos.Y + 2.5f, pos.Z + 1.0f),
                Color.Brown));
        }

        // Generate Boards between pillars (simplified as segments)
        // We'll place small cubes along the perimeter edges
        for (float t = -platformHalfSize; t <= platformHalfSize; t += 2.0f)
        {
            // Edge at Z = 15
            obstacles.Add(new Obstacle(
                new Vector3(t - 0.5f, 1.0f, platformHalfSize - 0.5f),
                new Vector3(t + 0.5f, 3.0f, platformHalfSize + 0.5f),
                Color.DarkGray));
            // Edge at Z = -15
            obstacles.Add(new Obstacle(
                new Vector3(t - 0.5f, 1.0f, -platformHalfSize - 0.5f),
                new Vector3(t + 0.5f, 3.0f, -platformHalfSize + 0.5f),
                Color.DarkGray));
            // Edge at X = 15
            obstacles.Add(new Obstacle(
                new Vector3(platformHalfSize - 0.5f, 1.0f, t - 0.5f),
                new Vector3(platformHalfSize + 0.5f, 3.0f, t + 0.5f),
                Color.DarkGray));
            // Edge at X = -15
            obstacles.Add(new Obstacle(
                new Vector3(-platformHalfSize - 0.5f, 1.0f, t - 0.5f),
                new Vector3(-platformHalfSize + 0.5f, 3.0f, t + 0.5f),
                Color.DarkGray));
        }

        while (!Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();

            // --- Update ---
            // Rotation logic (Mouse) - Relative to world space for camera orientation
            Vector2 mouseDelta = Raylib.GetMouseDelta();
            cameraRotationX += mouseDelta.X * 0.2f; // Yaw
            cameraRotationY -= mouseDelta.Y * 0.2f;   // Pitch

            if (cameraRotationY > 89.0f) cameraRotationY = 89.0f;
            if (cameraRotationY < -89.0f) cameraRotationY = -89.0f;

            float radX = cameraRotationX * (MathF.PI / 180.0f);
            float radY = cameraRotationY * (MathF.PI / 180.0f);

            // Calculate forward/right vectors relative to camera orientation
            float fwdX = MathF.Cos(radX) * MathF.Cos(radY);
            float fwdY = MathF.Sin(radY);
            float fwdZ = MathF.Sin(radX) * MathF.Cos(radY);

            // Right vector (perpendicular to forward and world up [0,1,0])
            float rightX = -fwdZ; 
            float rightY = 0.0f;
            float rightZ = fwdX;

            Vector3 moveDir = Vector3.Zero;
            if (Raylib.IsKeyDown(KeyboardKey.W)) moveDir += new Vector3(fwdX, 0, fwdZ); // Zeroed Y component
            if (Raylib.IsKeyDown(KeyboardKey.S)) moveDir -= new Vector3(fwdX, 0, fwdZ); // Zeroed Y component
            if (Raylib.IsKeyDown(KeyboardKey.A)) moveDir -= new Vector3(rightX, 0, rightZ); // Zeroed Y component
            if (Raylib.IsKeyDown(KeyboardKey.D)) moveDir += new Vector3(rightX, 0, rightZ); // Zeroed Y component

            if (moveDir != Vector3.Zero)
            {
                float length = (float)Math.Sqrt(moveDir.X * moveDir.X + moveDir.Y * moveDir.Y + moveDir.Z * moveDir.Z);
                if (length > 0)
                {
                    moveDir.X /= length;
                    moveDir.Y /= length;
                    moveDir.Z /= length;
                }
                playerPos += moveDir * moveSpeed;
            }

            // Gravity and Physics
            playerVel.Y += gravity * deltaTime; 
            playerPos += playerVel * deltaTime;   

            // Collision detection with platform bounds AND ground height
            bool onPlatform = Math.Abs(playerPos.X) <= platformHalfSize && 
                              Math.Abs(playerPos.Z) <= platformHalfSize;

            if (onPlatform && playerPos.Y < groundLevel + 1.0f) 
            {
                playerPos.Y = groundLevel + 1.0f;
                playerVel.Y = 0;
            }
            else
            {
                // If off platform, we don't reset vertical velocity (keep falling)
            }

            // Obstacle Collision Logic
            float sphereRadius = 1.0f;
            foreach (var obs in obstacles)
            {
                // Find closest point on AABB to the center of the player sphere
                float closestX = Math.Clamp(playerPos.X, obs.Min.X, obs.Max.X);
                float closestY = Math.Clamp(playerPos.Y, obs.Min.Y, obs.Max.Y);
                float closestZ = Math.Clamp(playerPos.Z, obs.Min.Z, obs.Max.Z);

                Vector3 closestPoint = new Vector3(closestX, closestY, closestZ);
                float distanceSq = (playerPos.X - closestPoint.X) * (playerPos.X - closestPoint.X) +
                                   (playerPos.Y - closestPoint.Y) * (playerPos.Y - closestPoint.Y) +
                                   (playerPos.Z - closestPoint.Z) * (playerPos.Z - closestPoint.Z);

                if (distanceSq < sphereRadius * sphereRadius)
                {
                    float distance = (float)Math.Sqrt(distanceSq);
                    // If the center is inside or very close to the surface, push out along the normal
                    // Simple approach: move player away from the closest point on the face
                    if (distance < 0.01f) // Inside obstacle - handle edge case
                    {
                        playerPos += new Vector3(0, 0.1f, 0); // Nudge up as a fallback
                    }
                    else
                    {
                        // Push out from the closest point
                        Vector3 normal = Vector3.Normalize(playerPos - closestPoint);
                        float penetration = sphereRadius - distance;
                        playerPos += normal * penetration;
                    }
                }
            }

            // Camera Follow Logic: Offset from the player's position
            float camDist = 8.0f;
            float camHeight = 4.0f;
            
            camera.Position.X = playerPos.X - fwdX * camDist;
            camera.Position.Y = playerPos.Y - fwdY * camDist + camHeight;
            camera.Position.Z = playerPos.Z - fwdZ * camDist;
            
            camera.Target = playerPos;

            rotationAngle += 2.0f; // Cube rotation

            // --- Draw ---
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.RayWhite);

            Raylib.BeginMode3D(camera);
                // Draw the ground plane
                Raylib.DrawModel(groundModel, new Vector3(0, 0, 0), 1.0f, Color.DarkGray);
                
                // Draw Obstacles
                foreach (var obs in obstacles)
                {
                    // Using a simple cube for pillars/boards since we have the mesh already
                    Raylib.DrawCube(new Vector3((obs.Min.X + obs.Max.X)/2, (obs.Min.Y + obs.Max.Y)/2, (obs.Min.Z + obs.Max.Z)/2), 
                                    obs.Max.X - obs.Min.X, obs.Max.Y - obs.Min.Y, obs.Max.Z - obs.Min.Z, 
                                    obs.Color);
                }

                // Draw the player character (blue sphere)
                Raylib.DrawModel(playerModel, playerPos, 1.0f, Color.Blue);

                // Draw the floating cube (red)
                Raylib.DrawModelEx(cubeModel, new Vector3(5.0f, 2.0f, 0), new Vector3(0, 1, 0), rotationAngle, new Vector3(1, 1, 1), Color.Red);
            Raylib.EndMode3D();

            Raylib.DrawText("Controls: WASD to move, Mouse to look", 10, 10, 20, Color.Black);
            Raylib.DrawFPS(screenWidth - 100, 10);
            
            Raylib.EndDrawing();
        }

        // Cleanup
        Raylib.UnloadModel(cubeModel);
        Raylib.UnloadModel(groundModel);
        Raylib.UnloadModel(playerModel);
        Raylib.CloseWindow();
    }
}
