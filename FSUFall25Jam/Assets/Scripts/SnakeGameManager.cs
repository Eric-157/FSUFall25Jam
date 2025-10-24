using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class SnakeGameManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public int gridWidth = 7;
    public int gridHeight = 7;
    public Vector3 origin = new Vector3(-100f, -79f, 0f); // bottom-left of playable area
    public float cellSizeX = 20f; // width of each grid cell in world units
    public float cellSizeY = 20f; // height of each grid cell in world units

    [Header("Prefabs")]
    public GameObject snakeSegmentPrefab;
    public GameObject foodPrefab;

    [Header("Sprites")]
    public Sprite headSprite;
    public Sprite bodySprite;
    public Sprite tailSprite;
    public Sprite turnSprite;

    [Header("Game Settings")]
    public float moveInterval = 0.25f;

    [Header("Walls")]
    public Tilemap wallTilemap;

    private List<Transform> snakeSegments = new List<Transform>();
    private List<Vector2Int> snakePositions = new List<Vector2Int>();
    private Vector2Int direction = Vector2Int.right;
    private float moveTimer;
    private Vector2Int foodPosition;
    private bool isGameOver = false;

    void Start()
    {
        StartNewGame();
    }

    void Update()
    {
        if (isGameOver)
        {
            if (Input.GetKeyDown(KeyCode.R))
                StartNewGame();
            return;
        }

        HandleInput();

        moveTimer += Time.deltaTime;
        if (moveTimer >= moveInterval)
        {
            MoveSnake();
            moveTimer = 0f;
        }
    }

    void HandleInput()
    {
        if ((Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) && direction != Vector2Int.down)
            direction = Vector2Int.up;
        else if ((Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) && direction != Vector2Int.up)
            direction = Vector2Int.down;
        else if ((Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) && direction != Vector2Int.right)
            direction = Vector2Int.left;
        else if ((Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) && direction != Vector2Int.left)
            direction = Vector2Int.right;
    }

    void StartNewGame()
    {
        foreach (Transform segment in snakeSegments)
            Destroy(segment.gameObject);
        snakeSegments.Clear();
        snakePositions.Clear();

        foreach (var food in GameObject.FindGameObjectsWithTag("Food"))
            Destroy(food);

        isGameOver = false;
        direction = Vector2Int.right;

        // Spawn head in center of grid
        Vector2Int startPos = new Vector2Int(gridWidth / 2, gridHeight / 2);
        snakePositions.Add(startPos);
        var head = Instantiate(snakeSegmentPrefab, GridToWorld(startPos), Quaternion.identity);
        snakeSegments.Add(head.transform);

        // Add 2 body segments behind
        for (int i = 1; i < 3; i++)
        {
            Vector2Int pos = startPos - new Vector2Int(i, 0);
            snakePositions.Add(pos);
            var segment = Instantiate(snakeSegmentPrefab, GridToWorld(pos), Quaternion.identity);
            snakeSegments.Add(segment.transform);
        }

        UpdateSnakeSprites();
        SpawnFood();
    }

    void MoveSnake()
    {
        Vector2Int newHeadPos = snakePositions[0] + direction;

        // Check bounds
        if (newHeadPos.x < 0 || newHeadPos.x >= gridWidth || newHeadPos.y < 0 || newHeadPos.y >= gridHeight)
        {
            GameOver();
            return;
        }

        // Self collision
        if (snakePositions.Contains(newHeadPos))
        {
            GameOver();
            return;
        }

        // Wall collision
        if (wallTilemap != null)
        {
            Vector3 worldPos = GridToWorld(newHeadPos);
            Vector3Int cellPos = wallTilemap.WorldToCell(worldPos);
            if (wallTilemap.HasTile(cellPos))
            {
                GameOver();
                return;
            }
        }

        // Move snake
        snakePositions.Insert(0, newHeadPos);
        snakePositions.RemoveAt(snakePositions.Count - 1);

        for (int i = 0; i < snakeSegments.Count; i++)
            snakeSegments[i].position = GridToWorld(snakePositions[i]);

        // Check food
        if (newHeadPos == foodPosition)
        {
            AddSegment();
            SpawnFood();
        }

        UpdateSnakeSprites();
    }

    void AddSegment()
    {
        Vector2Int tailDir = snakePositions[snakePositions.Count - 2] - snakePositions[snakePositions.Count - 1];
        Vector2Int newPos = snakePositions[snakePositions.Count - 1] - tailDir;

        var newSegment = Instantiate(snakeSegmentPrefab, GridToWorld(newPos), Quaternion.identity);
        snakeSegments.Add(newSegment.transform);
        snakePositions.Add(newPos);
    }

    void SpawnFood()
    {
        Vector2Int newFoodPos;
        do
        {
            newFoodPos = new Vector2Int(Random.Range(0, gridWidth), Random.Range(0, gridHeight));
        } while (snakePositions.Contains(newFoodPos) ||
                 (wallTilemap != null && wallTilemap.HasTile(wallTilemap.WorldToCell(GridToWorld(newFoodPos)))));

        var food = Instantiate(foodPrefab, GridToWorld(newFoodPos), Quaternion.identity);
        food.tag = "Food";
        foodPosition = newFoodPos;
    }

    void UpdateSnakeSprites()
    {
        for (int i = 0; i < snakeSegments.Count; i++)
        {
            SpriteRenderer sr = snakeSegments[i].GetComponent<SpriteRenderer>();
            sr.flipX = false;
            sr.flipY = false;

            if (i == 0)
            {
                // Head
                sr.sprite = headSprite;
                Vector2Int dir = snakePositions[0] - snakePositions[1];
                sr.transform.rotation = Quaternion.Euler(0, 0, DirectionToAngle(dir));
            }
            else if (i == snakeSegments.Count - 1)
            {
                // Tail
                sr.sprite = tailSprite;
                Vector2Int dir = snakePositions[i - 1] - snakePositions[i];
                sr.transform.rotation = Quaternion.Euler(0, 0, DirectionToAngle(dir));
            }
            else
            {
                // Body or Turn
                Vector2Int prevDir = snakePositions[i - 1] - snakePositions[i];
                Vector2Int nextDir = snakePositions[i] - snakePositions[i + 1];

                if (prevDir != nextDir)
                    SetTurnSprite(sr, prevDir, nextDir);
                else
                {
                    sr.sprite = bodySprite;
                    sr.transform.rotation = Quaternion.Euler(0, 0, DirectionToAngle(prevDir));
                }
            }
        }
    }

    void SetTurnSprite(SpriteRenderer sr, Vector2Int from, Vector2Int to)
    {
        sr.sprite = turnSprite;
        sr.flipX = false;
        sr.flipY = false;

        // Up → Right / Right → Up
        if ((from == Vector2Int.up && to == Vector2Int.right) || (from == Vector2Int.right && to == Vector2Int.up))
        {
            sr.transform.rotation = Quaternion.Euler(0, 0, 0);
        }
        // Down → Right / Right → Down
        else if ((from == Vector2Int.down && to == Vector2Int.right) || (from == Vector2Int.right && to == Vector2Int.down))
        {
            sr.transform.rotation = Quaternion.Euler(0, 0, 0);
            sr.flipY = true;
        }
        // Up → Left / Left → Up
        else if ((from == Vector2Int.up && to == Vector2Int.left) || (from == Vector2Int.left && to == Vector2Int.up))
        {
            sr.transform.rotation = Quaternion.Euler(0, 0, 0);
            sr.flipX = true;
        }
        // Down → Left / Left → Down
        else if ((from == Vector2Int.down && to == Vector2Int.left) || (from == Vector2Int.left && to == Vector2Int.down))
        {
            sr.transform.rotation = Quaternion.Euler(0, 0, 0);
            sr.flipX = true;
            sr.flipY = true;
        }
    }



    Vector3 GridToWorld(Vector2Int gridPos)
    {
        return new Vector3(origin.x + gridPos.x * cellSizeX, origin.y + gridPos.y * cellSizeY, 0f);
    }

    float DirectionToAngle(Vector2Int dir)
    {
        if (dir == Vector2Int.up) return 0;
        if (dir == Vector2Int.right) return -90;
        if (dir == Vector2Int.down) return 180;
        if (dir == Vector2Int.left) return 90;
        return 0;
    }

    void GameOver()
    {
        isGameOver = true;
        Debug.Log("Game Over! Press R to restart.");
    }
}
