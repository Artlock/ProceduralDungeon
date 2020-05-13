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
    [SerializeField, Range(1, 99)] private int minPathLength = 10;
    [SerializeField, Range(1, 99)] private int maxPathLength = 10;

    [SerializeField, Range(0f, 1f)] private float buildingDirectionChange;

    [Header("Debug")]
    [SerializeField, Range(0, 10)] private int debugPaddingSize = 4;
    [SerializeField] private bool printDebug = true;

    private List<ORIENTATION> directionsArray;

    private readonly List<Node> mainPath = new List<Node>();
    private int mainPathLength;

    private Vector2 buildingPosition;
    private ORIENTATION buildingDirection;
    private int toBuildRemaining;

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
        mainPath.Clear();
        mainPathLength = Random.Range(minPathLength, maxPathLength + 1);

        buildingPosition = new Vector2(0, 0);
        toBuildRemaining = mainPathLength;

        // First node at (0,0)
        mainPath.Add(GenerateNode());
        toBuildRemaining--;

        while (toBuildRemaining > 0)
        {
            UpdateBuildingPosDir(toBuildRemaining);

            mainPath.Add(GenerateNode());
            toBuildRemaining--;
        }

        // Debug printing
        if (printDebug) PrintMainPath();
    }

    private void PrintMainPath()
    {
        int maxX = Mathf.RoundToInt(mainPath.Select(x => x.position.x).Max());
        int maxY = Mathf.RoundToInt(mainPath.Select(x => x.position.y).Max());
        int minX = Mathf.RoundToInt(mainPath.Select(x => x.position.x).Min());
        int minY = Mathf.RoundToInt(mainPath.Select(x => x.position.y).Min());

        string debugString = "";

        for (int i = maxY; i >= minY; i--)
        {
            for (int j = minX; j <= maxX; j++)
            {
                Node n = mainPath.FirstOrDefault(x => x.position.x == j && x.position.y == i);

                if (n != null)
                {
                    int index = mainPath.IndexOf(n);
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

        Debug.Log("Path length : " + mainPath.Count);
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

    private void UpdateBuildingPosDir(int toBuildRemaining)
    {
        Vector2 newBuildingPosition;
        ORIENTATION newBuildingDirection;

        do
        {
            // TODO : Remove already explored directions to avoid exploring one that fails twice

            newBuildingDirection = RandomizeBuildingDirection(buildingDirection);
            newBuildingPosition = MoveIntoDirection(newBuildingDirection);
        }
        while (!IsPathPossible(new List<Vector2>(), newBuildingPosition, toBuildRemaining));

        buildingPosition = newBuildingPosition;
        buildingDirection = newBuildingDirection;
    }

    private Node GenerateNode()
    {
        return new Node(buildingPosition);
    }

    private Vector2 MoveIntoDirection(ORIENTATION buildingDirection)
    {
        switch (buildingDirection)
        {
            case ORIENTATION.NORTH: return new Vector2(buildingPosition.x, buildingPosition.y + 1);
            case ORIENTATION.EAST: return new Vector2(buildingPosition.x + 1, buildingPosition.y);
            case ORIENTATION.SOUTH: return new Vector2(buildingPosition.x, buildingPosition.y - 1);
            case ORIENTATION.WEST: return new Vector2(buildingPosition.x - 1, buildingPosition.y);
            default: return MoveIntoDirection(RandomizeBuildingDirection(buildingDirection));
        }
    }

    private ORIENTATION RandomizeBuildingDirection(ORIENTATION buildingDirection, bool fullRandom = false)
    {
        if (buildingDirection == ORIENTATION.NONE)
        {
            return directionsArray[Random.Range(1, directionsArray.Count)];
        }

        if (fullRandom)
            return directionsArray[Random.Range(1, directionsArray.Count)];
        else
        {
            if (Random.Range(0f, 100f) < buildingDirectionChange * 100f)
            {
                if (Random.Range(0f, 100f) >= 50f)
                {
                    return AngleToOrientation(OrientationToAngle(buildingDirection) + 90f);
                }
                else
                {
                    return AngleToOrientation(OrientationToAngle(buildingDirection) - 90f);
                }
            }
            else
            {
                return buildingDirection;
            }
        }
    }

    // Only takes into account MainPath and itself for now
    // Can add more conditions/paths considered later
    private bool IsPathPossible(List<Vector2> exploredPositions, Vector2 position, int depthRemaining)
    {
        // Check if the given position is possible
        if (mainPath.Any(x => x.position == position)
            || exploredPositions.Any(x => x == position))
        {
            return false;
        }

        depthRemaining--;
        if (depthRemaining == 0)
        {
            return true;
        }

        // Deep copy since we donc want to alter the other recursions
        // Happens after position check to avoid making a copy for nothing
        exploredPositions = ((Vector2[])exploredPositions.ToArray().Clone()).ToList();

        // Add explored item to our list
        // After the deep copy
        exploredPositions.Add(position);

        Vector2 newPos = new Vector2(position.x + 1, position.y);
        if (IsPathPossible(exploredPositions, newPos, depthRemaining))
        {
            return true;
        }
        newPos = new Vector2(position.x, position.y + 1);
        if (IsPathPossible(exploredPositions, newPos, depthRemaining))
        {
            return true;
        }
        newPos = new Vector2(position.x - 1, position.y);
        if (IsPathPossible(exploredPositions, newPos, depthRemaining))
        {
            return true;
        }
        newPos = new Vector2(position.x, position.y - 1);
        if (IsPathPossible(exploredPositions, newPos, depthRemaining))
        {
            return true;
        }

        // No possible paths found
        return false;
    }
}
