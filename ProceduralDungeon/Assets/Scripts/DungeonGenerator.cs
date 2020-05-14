using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Random = UnityEngine.Random;
using static Utils;

public class DungeonGenerator : MonoBehaviour
{
    private class Node
    {
        public Vector2 position;

        public Node(Vector2 buildingPosition)
        {
            position = buildingPosition;
        }
    }

    // More than that is too heavy since we make sure the path is possible using recursion
    [SerializeField, Range(10, 99)] private int minPathLength = 10;
    [SerializeField, Range(10, 99)] private int maxPathLength = 10;

    [SerializeField, Range(0f, 1f)] private float buildingDirectionChange;

    [Header("Debug")]
    [SerializeField, Range(0, 10)] private int debugPaddingSize = 4;
    [SerializeField] private bool printDebug = true;

    private List<ORIENTATION> directionsArray;

    private readonly List<Node> mainPath = new List<Node>();
    private int mainPathLength;

    private Vector2 buildingPosition;
    private ORIENTATION buildingDirection;

    private void Awake()
    {
        directionsArray = ((ORIENTATION[])Enum.GetValues(typeof(ORIENTATION))).ToList();

        if (maxPathLength < minPathLength)
            maxPathLength = minPathLength;
    }

    private void Start()
    {
        GenerateDungeon();
    }

    private void GenerateDungeon()
    {
        GenerateMainPath();

        // Debug printing
        if (printDebug)
        {
            PrintMainPath(mainPath);
        }
    }

    private void GenerateMainPath()
    {
        mainPath.Clear();
        mainPathLength = Random.Range(minPathLength, maxPathLength + 1);

        buildingPosition = new Vector2(0, 0);
        int mainPathToBuildRemaining = mainPathLength;

        // First node at (0,0)
        mainPath.Add(GenerateNode(buildingPosition));
        mainPathToBuildRemaining--;

        while (mainPathToBuildRemaining > 0)
        {
            // Returned int is the amount of rooms we wont be able to place anyway
            mainPathToBuildRemaining -= UpdateBuildingPosDir(mainPathToBuildRemaining);

            mainPath.Add(GenerateNode(buildingPosition));
            mainPathToBuildRemaining--;
        }
    }

    private void PrintMainPath(List<Node> toPrint)
    {
        int maxX = Mathf.RoundToInt(toPrint.Select(x => x.position.x).Max());
        int maxY = Mathf.RoundToInt(toPrint.Select(x => x.position.y).Max());
        int minX = Mathf.RoundToInt(toPrint.Select(x => x.position.x).Min());
        int minY = Mathf.RoundToInt(toPrint.Select(x => x.position.y).Min());

        string debugString = "";

        for (int i = maxY; i >= minY; i--)
        {
            for (int j = minX; j <= maxX; j++)
            {
                Node n = toPrint.FirstOrDefault(x => x.position.x == j && x.position.y == i);

                if (n != null)
                {
                    int index = toPrint.IndexOf(n);
                    string indexString = index.ToString().Trim();

                    if (index < 10)
                        indexString = "0" + indexString;

                    debugString += string.Format("{0}", PadBoth(indexString, debugPaddingSize));
                }
                else
                {
                    debugString += string.Format("{0}", PadBoth("- -", debugPaddingSize));
                }
            }

            debugString += "\n";
        }

        Debug.Log(debugString);

        Debug.Log("Path length : " + toPrint.Count);
        Debug.Log("Min X : " + minX);
        Debug.Log("Max X : " + maxX);
        Debug.Log("Min Y : " + minY);
        Debug.Log("Max Y : " + maxY);
    }

    public string PadBoth(string source, int length)
    {
        int spaces = length - source.Length;
        int padLeft = spaces / 2 + source.Length;
        return source.PadLeft(padLeft).PadRight(length);
    }

    private int UpdateBuildingPosDir(int toBuildRemaining)
    {
        Vector2 newBuildingPosition;
        ORIENTATION newBuildingDirection;

        // Ignores already explored directions to avoid exploring one that fails twice
        List<ORIENTATION> alreadyExploredDirections = new List<ORIENTATION>();
        alreadyExploredDirections.Add(OppositeOrientation(buildingDirection));

        // If no direction is possible for requested length pick the longest possible
        Tuple<bool, int> result;
        int smallestRemainingDepth = int.MaxValue;
        ORIENTATION fallbackDirection = ORIENTATION.NONE;
        bool fallback = false;

        do
        {
            newBuildingDirection = RandomizeBuildingDirection(buildingDirection, alreadyExploredDirections);
            alreadyExploredDirections.Add(newBuildingDirection);
            newBuildingPosition = MoveIntoDirection(newBuildingDirection);

            result = IsPathPossible(new List<Vector2>(), newBuildingPosition, toBuildRemaining);

            if (result.Item2 < smallestRemainingDepth)
            {
                smallestRemainingDepth = result.Item2;
                fallbackDirection = newBuildingDirection;
            }

            if (result.Item1)
            {
                break;
            }

            if (alreadyExploredDirections.Count == directionsArray.Count)
            {
                fallback = true;

                break;
            }
        }
        while (!result.Item1);

        if (fallback)
        {
            buildingDirection = fallbackDirection;
            buildingPosition = MoveIntoDirection(buildingDirection);
        }
        else
        {
            buildingPosition = newBuildingPosition;
            buildingDirection = newBuildingDirection;
        }

        return smallestRemainingDepth;
    }

    private Node GenerateNode(Vector2 position)
    {
        return new Node(position);
    }

    private Vector2 MoveIntoDirection(ORIENTATION buildingDirection)
    {
        switch (buildingDirection)
        {
            case ORIENTATION.NORTH: return new Vector2(buildingPosition.x, buildingPosition.y + 1);
            case ORIENTATION.EAST: return new Vector2(buildingPosition.x + 1, buildingPosition.y);
            case ORIENTATION.SOUTH: return new Vector2(buildingPosition.x, buildingPosition.y - 1);
            case ORIENTATION.WEST: return new Vector2(buildingPosition.x - 1, buildingPosition.y);
            default: return MoveIntoDirection(RandomizeBuildingDirection(buildingDirection, new List<ORIENTATION>()));
        }
    }

    private ORIENTATION RandomizeBuildingDirection(ORIENTATION prevBuildingDirection, List<ORIENTATION> alreadyExplored, bool fullRandom = false)
    {
        List<ORIENTATION> directionsAvailable = directionsArray.Except(alreadyExplored).ToList();

        if (prevBuildingDirection == ORIENTATION.NONE)
        {
            return directionsAvailable[Random.Range(0, directionsAvailable.Count)];
        }

        if (fullRandom)
            return directionsAvailable[Random.Range(0, directionsAvailable.Count)];
        else
        {
            if (directionsAvailable.Count == 1)
            {
                return directionsAvailable[0];
            }
            else if (directionsAvailable.FirstOrDefault(x => x == prevBuildingDirection) != ORIENTATION.NONE)
            {
                // Available includes same direction and turn
                if (Random.Range(0f, 100f) < buildingDirectionChange * 100f)
                {
                    // Random turn or only available turn
                    List<ORIENTATION> remainingDirs = directionsAvailable.Where(x => x != prevBuildingDirection).ToList();
                    return remainingDirs[Random.Range(0, remainingDirs.Count)];
                }
                else
                {
                    // Same direction as before
                    return prevBuildingDirection;
                }
            }
            else
            {
                // Pick either of available at random (Only sides left)
                return directionsAvailable[Random.Range(0, directionsAvailable.Count)];
            }
        }
    }

    // Only takes into account MainPath and itself for now
    // Can add more conditions/paths considered later
    private Tuple<bool, int> IsPathPossible(List<Vector2> exploredPositions, Vector2 position, int depthRemaining)
    {
        // Check if the given position is possible
        if (mainPath.Any(x => x.position == position)
            || exploredPositions.Any(x => x == position))
        {
            return new Tuple<bool, int>(false, depthRemaining);
        }

        depthRemaining--;
        if (depthRemaining == 0)
        {
            return new Tuple<bool, int>(true, depthRemaining);
        }

        // Deep copy since we donc want to alter the other recursions
        // Happens after position check to avoid making a copy for nothing
        exploredPositions = ((Vector2[])exploredPositions.ToArray().Clone()).ToList();

        // Add explored item to our list
        // After the deep copy
        exploredPositions.Add(position);

        int smallestRemainingDepth = int.MaxValue;
        Tuple<bool, int> result;
        Vector2 newPos;

        newPos = new Vector2(position.x + 1, position.y);
        result = IsPathPossible(exploredPositions, newPos, depthRemaining);
        if (result.Item2 < smallestRemainingDepth) smallestRemainingDepth = result.Item2;
        if (result.Item1) return new Tuple<bool, int>(true, smallestRemainingDepth);

        newPos = new Vector2(position.x, position.y + 1);
        result = IsPathPossible(exploredPositions, newPos, depthRemaining);
        if (result.Item2 < smallestRemainingDepth) smallestRemainingDepth = result.Item2;
        if (result.Item1) return new Tuple<bool, int>(true, smallestRemainingDepth);

        newPos = new Vector2(position.x - 1, position.y);
        result = IsPathPossible(exploredPositions, newPos, depthRemaining);
        if (result.Item2 < smallestRemainingDepth) smallestRemainingDepth = result.Item2;
        if (result.Item1) return new Tuple<bool, int>(true, smallestRemainingDepth);

        newPos = new Vector2(position.x, position.y - 1);
        result = IsPathPossible(exploredPositions, newPos, depthRemaining);
        if (result.Item2 < smallestRemainingDepth) smallestRemainingDepth = result.Item2;
        if (result.Item1) return new Tuple<bool, int>(true, smallestRemainingDepth);

        // No possible paths found
        return new Tuple<bool, int>(false, smallestRemainingDepth);
    }

    // To get deepest room (Where to place key)
    // Also know which rooms are inside a secondary path (Check if two secondary paths lead to the same path)
    /*

foreach (room in mainRooms)
{
    List<Room> visitedRooms = new List<Room>();
    
    foreach (subRoom in room.connectedSubRooms)
    {
        if (!visitedRooms.Contains(subRoom))
        {
            List<Room> subRooms = new List<Room> ();
            Tuple<Room, int> deepestSubRoom = Explore (subRooms, subRoom, 0);
            PlaceKey (deepestSubRoom); // Place a key at the deepest level
            
            foreach (room in subRooms)
            {
                if (!visitedRooms.Contains(room))
                {
                    visitedRooms.Add(room);
                }
            }
        }
    }
}

...

// Method to get the deepest room

Tuple<Room, int> GetDeepestRoom (List<Room> exploredRooms, Room currentRoom, int depth)
{
    exploredRooms.Add (currentRoom, depth);
    
    Tuple<Room, int> deepestRoom = new Tuple<Room, int>(currentRoom, depth);
    
    foreach (nextRoom in currentRoom.connectedSubRooms)
    {
        if (!exploredRooms.Contains(nextRoom))
        {
            Tuple<Room, int> tempDeepestRoom = GetDeepestRoom (exploredRooms, nextRoom, depth + 1);
          
            if (tempDeepestRoom.Value2 > deepestRoom.Value2)
            {
                deepestRoom = tempDeepestRoom;
            }
        }
    }
    
    return deepestRoom;
}

    */
}
