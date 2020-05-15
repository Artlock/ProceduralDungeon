﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Random = UnityEngine.Random;
using static Utils;
using CreativeSpore.SuperTilemapEditor;

public class DungeonGenerator : MonoBehaviour
{
    public class Node
    {
        // Do not change externally
        public Vector2 position { get; private set; }
        // Do not change externally
        public Dictionary<ORIENTATION, Node> mainPath { get; private set; } = new Dictionary<ORIENTATION, Node>();
        // Do not change externally
        public Dictionary<ORIENTATION, List<Node>> secondaryPaths { get; private set; } = new Dictionary<ORIENTATION, List<Node>>();

        public bool IsKeyNode { get; private set; }

        public Node(Vector2 buildingPosition)
        {
            position = buildingPosition;
        }

        public void AddMainPath(ORIENTATION orient, Node node)
        {
            mainPath.Add(orient, node);
        }

        public void AddSecondaryPath(ORIENTATION orient, List<Node> secondaryPath)
        {
            secondaryPaths.Add(orient, secondaryPath);
        }

        public List<ORIENTATION> GetAvailableOrientations()
        {
            List<ORIENTATION> available = new List<ORIENTATION>();

            available.AddRange(directionsList.Where(x => x != ORIENTATION.NONE && !mainPath.Keys.Contains(x) && !secondaryPaths.Keys.Contains(x)));

            return available;
        }

        public List<ORIENTATION> GetDoorOrientations()
        {
            List<ORIENTATION> available = new List<ORIENTATION>();

            available.AddRange(mainPath.Keys);
            available.AddRange(secondaryPaths.Keys);

            return available;
        }

        public void SetKeyNode()
        {
            IsKeyNode = true;
        }
    }

    // More than that is too heavy since we make sure the path is possible using recursion
    [Header("Main Path")]
    [SerializeField, Range(10, 99)] private int minMainPathLength = 10;
    [SerializeField, Range(10, 99)] private int maxMainPathLength = 10;

    [Header("Secondary Paths")]
    [SerializeField, Range(1, 10)] private int minRoomsBeforeNewSecondary = 1;
    [SerializeField, Range(1, 10)] private int maxRoomsBeforeNewSecondary = 1;
    [SerializeField, Range(2, 10)] private int minSecondaryPathLength = 5;
    [SerializeField, Range(2, 10)] private int maxSecondaryPathLength = 5;

    [Header("General Settings")]
    [SerializeField] private bool failIfTooShort = false;
    [SerializeField, Range(0f, 1f)] private float buildingDirectionChange;

    [Header("Available Rooms")]
    [SerializeField] private List<Room> rooms = new List<Room>();

    [Header("Debug")]
    [SerializeField, Range(0, 10)] private int debugPaddingSize = 4;
    [SerializeField] private bool printDebug = true;

    // Do not change externally
    public static List<ORIENTATION> directionsList { get; private set; }

    private readonly List<Node> mainPath = new List<Node>();
    private readonly List<List<Node>> secondaryPaths = new List<List<Node>>();

    #region Monobehaviours

    private void Awake()
    {
        directionsList = ((ORIENTATION[])Enum.GetValues(typeof(ORIENTATION))).ToList();

        if (maxMainPathLength < minMainPathLength)
            maxMainPathLength = minMainPathLength;

        if (maxMainPathLength < maxRoomsBeforeNewSecondary)
            maxRoomsBeforeNewSecondary = maxMainPathLength;

        if (maxRoomsBeforeNewSecondary < minRoomsBeforeNewSecondary)
            maxRoomsBeforeNewSecondary = minRoomsBeforeNewSecondary;
    }

    private void Start()
    {
        GenerateDungeon();
        MaterializeDungeon();
    }

    #endregion

    #region Materialization

    private void MaterializeDungeon()
    {
        // Select all room prefabs
        List<Room> roomComponents = new List<Room>();
        roomComponents.AddRange(rooms.SelectMany(x => x.GetComponentsInChildren<Room>()));

        // Recursive creation
        MaterializeWing(mainPath, roomComponents);
    }

    private void MaterializeWing(List<Node> wing, List<Room> possibleRooms)
    {
        Room room;
        Vector3 realPos;

        foreach (Node node in wing)
        {
            room = GetCorrectRoom(node, possibleRooms);
            realPos = ConvertNodeToWorld(node, room);
            InstantiateRoom(room.gameObject, realPos, node.position);

            foreach (KeyValuePair<ORIENTATION, List<Node>> subPaths in node.secondaryPaths)
            {
                MaterializeWing(subPaths.Value, possibleRooms);
            }
        }
    }

    private Vector3 ConvertNodeToWorld(Node node, Room room)
    {
        TilemapGroup tmg = room.gameObject.GetComponent<TilemapGroup>();

        return tmg.Tilemaps[0].MapBounds.center + new Vector3(node.position.x * tmg.Tilemaps[0].MapBounds.extents.x, node.position.x * tmg.Tilemaps[0].MapBounds.extents.y);
    }

    private Room GetCorrectRoom(Node node, List<Room> possibleRooms)
    {
        List<ORIENTATION> requiredDoorOrients = node.GetDoorOrientations();

        if (node.IsKeyNode)
            possibleRooms = possibleRooms.Where(x => x.GetComponentInChildren<KeyCollectible>() != null).ToList();

        possibleRooms = possibleRooms.Where(x =>
        {
            List<ORIENTATION> doorOrients = x.GetComponentsInChildren<Door>().Select(y => AngleToOrientation(-y.transform.eulerAngles.z)).ToList();

            if (doorOrients.Count == requiredDoorOrients.Count)
            {
                foreach (ORIENTATION ori in requiredDoorOrients)
                {
                    if (!doorOrients.Contains(ori))
                    {
                        return false;
                    }
                }

                return true;
            }
            else
            {
                return false;
            }
        }).ToList();

        return possibleRooms[Random.Range(0, possibleRooms.Count)];
    }

    private void InstantiateRoom(GameObject prefab, Vector3 realPos, Vector2 nodePos)
    {
        GameObject ga = Instantiate(prefab, realPos, Quaternion.identity);
        ga.GetComponentInChildren<Room>().position = new Vector2Int((int)nodePos.x, (int)nodePos.y);
    }

    #endregion

    #region Nodes Generation

    private void GenerateDungeon()
    {
        // Main path generation

        mainPath.Clear();
        int mainPathLength = Random.Range(minMainPathLength, maxMainPathLength + 1);

        Vector3 buildingPosition = new Vector2(0, 0);
        ORIENTATION buildingDirection = ORIENTATION.NONE;

        bool success = GeneratePath(true, mainPath, mainPathLength, buildingPosition, buildingDirection);

        if (!success) Debug.LogError("Main path failed. Everying stopped");

        // Secondary paths generation

        int currentSecondaryProgressionIndex = Random.Range(minRoomsBeforeNewSecondary, maxRoomsBeforeNewSecondary);

        while (currentSecondaryProgressionIndex < mainPath.Count)
        {
            List<ORIENTATION> availableOrientations = mainPath[currentSecondaryProgressionIndex].GetAvailableOrientations();

            if (availableOrientations.Count == 0)
            {
                currentSecondaryProgressionIndex++;

                continue;
            }

            List<Node> secondaryPath = new List<Node>();
            int secondaryPathLength = Random.Range(minSecondaryPathLength, maxSecondaryPathLength + 1);

            buildingPosition = mainPath[currentSecondaryProgressionIndex].position;
            buildingDirection = availableOrientations[Random.Range(0, availableOrientations.Count)];

            success = GeneratePath(false, secondaryPath, secondaryPathLength, buildingPosition, buildingDirection, directionsList.Where(x => !availableOrientations.Contains(x)).ToList());

            if (success)
            {
                secondaryPaths.Add(secondaryPath);

                // Tell main path we've added a secondary path by telling him he now has a secondary door
                mainPath[currentSecondaryProgressionIndex].AddSecondaryPath(buildingDirection, secondaryPath);

                currentSecondaryProgressionIndex += Random.Range(minRoomsBeforeNewSecondary, maxRoomsBeforeNewSecondary);

                List<Node> subNodes = new List<Node>();
                Tuple<Node, int> deepestSubRoom = GetDeepestNode(subNodes, secondaryPath[0], 0);
                deepestSubRoom.Item1.SetKeyNode();
            }
            else
                currentSecondaryProgressionIndex++;
        }

        // Debug printing
        if (printDebug)
        {
            PrintMainPath(mainPath);
        }
    }

    private bool GeneratePath(bool isMain, List<Node> pathList, int pathLength, Vector2 startPos, ORIENTATION startDir, List<ORIENTATION> alreadyExploredDirs = null)
    {
        int pathToBuildRemaining = pathLength;

        Vector2 currentPos = startPos;
        ORIENTATION currentDir = startDir;

        // Returned int is the amount of rooms we wont be able to place anyway
        Tuple<int, PossibleBuild> tuple = GetPossibleBuildAmountAndPosDir(pathToBuildRemaining, currentDir, currentPos, alreadyExploredDirs);

        if (failIfTooShort && tuple.Item1 > 0)
        {
            return false;
        }

        if (isMain)
        {
            pathList.Add(GenerateNode(currentPos));
            pathToBuildRemaining--;
        }

        while (pathToBuildRemaining > 0)
        {
            // Returned int is the amount of rooms we wont be able to place anyway
            tuple = GetPossibleBuildAmountAndPosDir(pathToBuildRemaining, currentDir, currentPos);
            pathToBuildRemaining -= tuple.Item1;

            currentPos = tuple.Item2.pos;
            currentDir = tuple.Item2.dir;

            pathList.Add(GenerateNode(currentPos));

            // Tell previous node of new door
            if (pathList.Count >= 2)
                pathList[pathList.Count - 2].AddMainPath(currentDir, pathList[pathList.Count - 2]);

            // Tell new node of previous door
            pathList[pathList.Count - 1].AddMainPath(OppositeOrientation(currentDir), pathList[pathList.Count - 1]);

            pathToBuildRemaining--;
        }

        return true;
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

    public struct PossibleBuild
    {
        public Vector2 pos;
        public ORIENTATION dir;

        public PossibleBuild(Vector2 newBuildingPosition, ORIENTATION newBuildingDirection)
        {
            pos = newBuildingPosition;
            dir = newBuildingDirection;
        }
    }

    private Tuple<int, PossibleBuild> GetPossibleBuildAmountAndPosDir(int toBuildRemaining, ORIENTATION buildOrientPrev, Vector2 buildPosPrev, List<ORIENTATION> alreadyExploredDirections = null)
    {
        // Ignores already explored directions to avoid exploring one that fails twice
        if (alreadyExploredDirections == null)
        {
            alreadyExploredDirections = new List<ORIENTATION>();
        }

        alreadyExploredDirections.Add(OppositeOrientation(buildOrientPrev));
        if (!alreadyExploredDirections.Contains(ORIENTATION.NONE)) alreadyExploredDirections.Add(ORIENTATION.NONE);

        // If no direction is possible for requested length pick the longest possible
        Tuple<bool, int> result;
        int smallestRemainingDepth = int.MaxValue;
        ORIENTATION fallbackDirection = ORIENTATION.NONE;
        bool fallback = false;

        Vector2 newBuildingPosition;
        ORIENTATION newBuildingDirection;

        do
        {
            newBuildingDirection = RandomizeBuildingDirection(buildOrientPrev, alreadyExploredDirections);
            alreadyExploredDirections.Add(newBuildingDirection);
            newBuildingPosition = MoveIntoDirection(newBuildingDirection, buildPosPrev);

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

            if (alreadyExploredDirections.Count == directionsList.Count)
            {
                fallback = true;

                break;
            }
        }
        while (!result.Item1);

        if (fallback)
        {
            return new Tuple<int, PossibleBuild>(smallestRemainingDepth, new PossibleBuild(MoveIntoDirection(fallbackDirection, buildPosPrev), fallbackDirection));
        }
        else
        {
            return new Tuple<int, PossibleBuild>(smallestRemainingDepth, new PossibleBuild(newBuildingPosition, newBuildingDirection));
        }
    }

    private Node GenerateNode(Vector2 position)
    {
        Node n = new Node(position);
        return n;
    }

    private Vector2 MoveIntoDirection(ORIENTATION buildingDirection, Vector2 buildingPosition)
    {
        switch (buildingDirection)
        {
            case ORIENTATION.NORTH: return new Vector2(buildingPosition.x, buildingPosition.y + 1);
            case ORIENTATION.EAST: return new Vector2(buildingPosition.x + 1, buildingPosition.y);
            case ORIENTATION.SOUTH: return new Vector2(buildingPosition.x, buildingPosition.y - 1);
            case ORIENTATION.WEST: return new Vector2(buildingPosition.x - 1, buildingPosition.y);
            default: return MoveIntoDirection(RandomizeBuildingDirection(buildingDirection, new List<ORIENTATION>()), buildingPosition);
        }
    }

    private ORIENTATION RandomizeBuildingDirection(ORIENTATION prevBuildingDirection, List<ORIENTATION> alreadyExplored, bool fullRandom = false)
    {
        List<ORIENTATION> directionsAvailable = directionsList.Except(alreadyExplored).ToList();

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
            || secondaryPaths.SelectMany(x => x).Any(x => x.position == position)
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

    #endregion

    // To get deepest node (Where to place key)
    // Also know which rooms are inside a secondary path (Check if two secondary paths are connected)
    // Useful for circular secondary paths, but can also be used for linear ones
    private Tuple<Node, int> GetDeepestNode(List<Node> exploredNodes, Node currentNode, int depth)
    {
        exploredNodes.Add(currentNode);

        Tuple<Node, int> deepestNode = new Tuple<Node, int>(currentNode, depth);

        foreach (KeyValuePair<ORIENTATION, Node> kp in currentNode.mainPath)
        {
            if (!exploredNodes.Contains(kp.Value))
            {
                Tuple<Node, int> tempDeepestNode = GetDeepestNode(exploredNodes, kp.Value, depth + 1);

                if (tempDeepestNode.Item2 > deepestNode.Item2)
                {
                    deepestNode = tempDeepestNode;
                }
            }
        }

        return deepestNode;
    }
}