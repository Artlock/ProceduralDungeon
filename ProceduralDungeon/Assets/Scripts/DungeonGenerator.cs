using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using static Utils;
using CreativeSpore.SuperTilemapEditor;

public class DungeonGenerator : MonoBehaviour
{
    public class Node
    {
        public Vector2 position;
        public int pathID = 0;
        public bool hasKey = false;
        public List<ORIENTATION> locks = new List<ORIENTATION>();

        public Dictionary<ORIENTATION, Node> doors = new Dictionary<ORIENTATION, Node>();

        public Node(Vector2 position, int pathID, bool hasKey)
        {
            this.position = position;
            this.pathID = pathID;
            this.hasKey = hasKey;
        }

        public void SetLocks(List<ORIENTATION> locks)
        {
            this.locks = locks;
        }

        public List<ORIENTATION> GetAvailableOrientations()
        {
            return existingDirections.Where(x => !doors.Keys.Contains(x)).ToList();
        }

        public List<ORIENTATION> GetOrientationsWithDoors()
        {
            return doors.Keys.ToList();
        }

        public List<ORIENTATION> GetOrientationsWithoutDoors()
        {
            return existingDirections.Except(doors.Keys.ToList()).ToList();
        }

        public List<Node> GetConnectedNodes()
        {
            return doors.Select(x => x.Value).ToList();
        }

        public List<Node> GetConnectedNodes_SamePath()
        {
            return doors.Where(x => x.Value.pathID == pathID).Select(x => x.Value).ToList();
        }

        public List<Node> GetConnectedNodes_DifferentPath()
        {
            return doors.Where(x => x.Value.pathID != pathID).Select(x => x.Value).ToList();
        }

        public List<Node> GetConnectedNodes_SpecificPath(int pathID)
        {
            return doors.Where(x => x.Value.pathID == pathID).Select(x => x.Value).ToList();
        }
    }

    public struct PossibleBuild
    {
        public Vector2 newPos;
        public ORIENTATION dirToGetThere;

        public PossibleBuild(Vector2 newPos, ORIENTATION dirToGetThere)
        {
            this.newPos = newPos;
            this.dirToGetThere = dirToGetThere;
        }
    }

    #region Parameters

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
    [SerializeField] private bool failIfPossiblePathTooShort = false;
    [SerializeField, Range(0f, 1f)] private float buildingDirectionChangeChance = 0.3f;

    [Header("Available Rooms")]
    [SerializeField] private List<Room> roomPrefabs = new List<Room>();

    public static List<ORIENTATION> existingDirections;
    public static List<ORIENTATION> existingDirectionsFull;

    private readonly List<Node> nodes = new List<Node>();
    private readonly Dictionary<int, List<Node>> paths = new Dictionary<int, List<Node>>();

    #endregion

    #region Monobehaviours

    private void Awake()
    {
        existingDirections = ((ORIENTATION[])Enum.GetValues(typeof(ORIENTATION))).Where(x => x != ORIENTATION.NONE).ToList();
        existingDirectionsFull = ((ORIENTATION[])Enum.GetValues(typeof(ORIENTATION))).ToList();

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
        MaterializeDungeon(roomPrefabs.Select(x => x.GetComponentInChildren<Room>()).ToList());
    }

    #endregion

    #region Materialization

    private void MaterializeDungeon(List<Room> possibleRooms)
    {
        foreach(Node node in nodes)
        {
            Room room = GetFittingRoom(node, possibleRooms);
            Vector3 roomPosition = NodeToWorldPosition(node, room);
            InstantiateRoom(room.gameObject, roomPosition, node.position);
        }
    }

    private Vector3 NodeToWorldPosition(Node node, Room room)
    {
        TilemapGroup tmg = room.gameObject.GetComponent<TilemapGroup>();
        return new Vector3(node.position.x * tmg.Tilemaps[0].MapBounds.size.x, node.position.y * tmg.Tilemaps[0].MapBounds.size.y);
    }

    private void InstantiateRoom(GameObject prefab, Vector3 realPos, Vector2 nodePos)
    {
        GameObject ga = Instantiate(prefab, realPos, Quaternion.identity);
        ga.GetComponentInChildren<Room>().position = new Vector2Int((int)nodePos.x, (int)nodePos.y);
    }

    private Room GetFittingRoom(Node node, List<Room> possibleRooms)
    {
        List<ORIENTATION> requiredDoorOrients = node.GetOrientationsWithDoors();

        // Key state sorting
        if (node.hasKey)
            possibleRooms = possibleRooms.Where(x => x.GetComponentInChildren<KeyCollectible>() != null).ToList();
        else
            possibleRooms = possibleRooms.Where(x => x.GetComponentInChildren<KeyCollectible>() == null).ToList();

        // Door position sorting
        possibleRooms = possibleRooms.Where(x =>
        {
            List<ORIENTATION> availableDoorOrients = x.GetComponentsInChildren<Door>().Select(y => AngleToOrientation(-y.transform.eulerAngles.z)).ToList();

            if (availableDoorOrients.Count == requiredDoorOrients.Count)
            {
                foreach (ORIENTATION ori in requiredDoorOrients)
                {
                    if (!availableDoorOrients.Contains(ori))
                        return false;
                }

                return true;
            }
            else
            {
                return false;
            }
        }).ToList();

        // Lock position sorting
        if (node.locks.Count == 0)
            possibleRooms = possibleRooms.Where(x => x.GetComponentsInChildren<Door>().All(y => y.State == Door.STATE.OPEN)).ToList();
        else
            possibleRooms = possibleRooms.Where(x =>
            {
                bool correctDoors = true;

                x.GetComponentsInChildren<Door>().ToList().ForEach(y =>
                {
                    if (node.locks.Contains(y.Orientation) && y.State != Door.STATE.CLOSED)
                        correctDoors = false;
                    else if (!node.locks.Contains(y.Orientation) && y.State != Door.STATE.OPEN)
                        correctDoors = false;
                });

                return correctDoors;

            }).ToList();

        return possibleRooms[Random.Range(0, possibleRooms.Count)];
    }

    #endregion

    #region Nodes Generation

    private void GenerateDungeon()
    {
        // Main path generation

        int mainPathLength = Random.Range(minMainPathLength, maxMainPathLength + 1);

        Vector3 buildingPosition = new Vector2(0, 0);
        ORIENTATION buildingDirection = ORIENTATION.NONE;

        Node firstNode = new Node(buildingPosition, 0, false);

        // Add to nodes list
        nodes.Add(firstNode);

        // Add to dictionary of paths
        if (!paths.ContainsKey(0))
            paths.Add(0, new List<Node>());
        paths[0].Add(firstNode);

        bool success = GeneratePath(0, mainPathLength, buildingPosition, buildingDirection);

        if (!success) Debug.LogError("Main path failed. Everything stopped");

        // Secondary paths generation

        int secondaryPathsCount = 0;
        int progressionIndex = Random.Range(minRoomsBeforeNewSecondary, maxRoomsBeforeNewSecondary);

        while (progressionIndex < paths[0].Count)
        {
            List<ORIENTATION> availableOrientations = paths[0][progressionIndex].GetAvailableOrientations();

            if (availableOrientations.Count == 0)
            {
                progressionIndex++;
                continue;
            }

            int secondaryPathLength = Random.Range(minSecondaryPathLength, maxSecondaryPathLength + 1);

            buildingPosition = paths[0][progressionIndex].position;
            buildingDirection = availableOrientations[Random.Range(0, availableOrientations.Count)];

            success = GeneratePath(secondaryPathsCount + 1, secondaryPathLength, buildingPosition, buildingDirection);

            if (success)
                progressionIndex += Random.Range(minRoomsBeforeNewSecondary, maxRoomsBeforeNewSecondary);
            else
                progressionIndex++;
        }
    }

    // Can only generate linear paths possibly connected to a parent path
    private bool GeneratePath(int pathID, int pathLength, Vector2 startPos, ORIENTATION startDir)
    {
        int pathToBuildRemaining = pathLength;

        Vector2 currentPos = startPos;
        ORIENTATION currentDir = startDir;

        // This can be null
        Node buildFromNode = nodes.FirstOrDefault(x => x.position == currentPos);

        // Returned int is the amount of rooms we wont be able to place anyway
        Tuple<PossibleBuild, int> tuple = GetPossibleBuild(pathToBuildRemaining, currentDir, currentPos, buildFromNode?.GetOrientationsWithoutDoors());

        if (tuple.Item2 > 0)
        {
            if (failIfPossiblePathTooShort)
            {
                return false;
            }
            else
            {
                // Remove amount of rooms we wont be able to build
                pathToBuildRemaining -= tuple.Item2;
            }
        }

        while (pathToBuildRemaining > 0)
        {
            pathToBuildRemaining--;

            buildFromNode = nodes.FirstOrDefault(x => x.position == currentPos);

            // Returned int is the amount of rooms we wont be able to place anyway
            tuple = GetPossibleBuild(pathToBuildRemaining, currentDir, currentPos, buildFromNode?.GetOrientationsWithoutDoors());

            currentPos = tuple.Item1.newPos;
            currentDir = tuple.Item1.dirToGetThere;

            Node newNode = new Node(currentPos, pathID, pathToBuildRemaining == 0 && pathID != 0);

            // Add to nodes list
            nodes.Add(newNode);

            // Add to dictionary of paths
            if (!paths.ContainsKey(pathID))
                paths.Add(pathID, new List<Node>());
            paths[pathID].Add(newNode);

            if (buildFromNode != null)
            {
                buildFromNode.doors.Add(currentDir, newNode);
                newNode.doors.Add(OppositeOrientation(currentDir), buildFromNode);
            }
        }

        return true;
    }

    private Tuple<PossibleBuild, int> GetPossibleBuild(int toBuildLeft, ORIENTATION previousDirection, Vector2 previousPosition, List<ORIENTATION> available)
    {
        List<ORIENTATION> directionsAvailable = available != null ? available : existingDirections.ToList();
        // Ignore direction we built from before
        directionsAvailable.Remove(OppositeOrientation(previousDirection));

        Tuple<bool, int> result;
        int smallestRemainingDepth = int.MaxValue;

        bool fallback = false;
        Vector2 fallbackPosition = new Vector2();
        ORIENTATION fallbackDirection = ORIENTATION.NONE;

        Vector2 newPosition;
        ORIENTATION newDirection;

        do
        {
            newDirection = RandomizeDirection(previousDirection, directionsAvailable);
            newPosition = MoveIntoDirection(newDirection, previousPosition);

            directionsAvailable.Remove(newDirection);

            result = IsCellAvailable(new List<Vector2>(), newPosition, toBuildLeft);

            if (result.Item2 < smallestRemainingDepth)
            {
                smallestRemainingDepth = result.Item2;

                fallbackDirection = newDirection;
                fallbackPosition = newPosition;
            }

            if (result.Item1)
                break;

            if (directionsAvailable.Count == 0)
            {
                fallback = true;
                break;
            }
        }
        while (!result.Item1);

        // If no direction is possible for requested length pick the longest possible
        if (fallback)
            return new Tuple<PossibleBuild, int>(new PossibleBuild(fallbackPosition, fallbackDirection), smallestRemainingDepth);
        else
            return new Tuple<PossibleBuild, int>(new PossibleBuild(newPosition, newDirection), 0);
    }

    // Not real random but rather same direction as before or a chance to turn 90 degrees
    // Unless we are fed a ORIENTATION.NONE in which case the result can be anything available
    // If nothing is available returns ORIENTATION.NONE
    private ORIENTATION RandomizeDirection(ORIENTATION previousDirection, List<ORIENTATION> available)
    {
        List<ORIENTATION> directionsAvailable = available;
        // Ignore direction we built from before
        directionsAvailable.Remove(OppositeOrientation(previousDirection));

        // Default behaviours
        if (directionsAvailable.Count == 0)
            return ORIENTATION.NONE; // Fail (Manage on the other side)
        if (previousDirection == ORIENTATION.NONE)
            return directionsAvailable[Random.Range(0, directionsAvailable.Count)];

        // 1-3 directions possible onwards (forward/left/right)

        // Generate turns from our previous direction
        List<ORIENTATION> sidesAvailable = new List<ORIENTATION>();
        sidesAvailable.Add(AngleToOrientation(OrientationToAngle(previousDirection) + 90f)); // Right
        sidesAvailable.Add(AngleToOrientation(OrientationToAngle(previousDirection) - 90f)); // Left

        // Only keep available sides
        sidesAvailable = sidesAvailable.Where(x => directionsAvailable.Contains(x)).ToList();

        if (sidesAvailable.Count == 0)
            return previousDirection; // Has to be the only one remaining
        else if (directionsAvailable.Contains(previousDirection))
        {
            // 0-2 sides and forward remaining

            if (Random.Range(0f, 100f) < buildingDirectionChangeChance * 100f)
                return sidesAvailable[Random.Range(0, sidesAvailable.Count)];
            else
                return previousDirection;
        }
        else
            return sidesAvailable[Random.Range(0, sidesAvailable.Count)]; // 0-2 sides remaining
    }

    private Vector2 MoveIntoDirection(ORIENTATION direction, Vector2 startPosition)
    {
        switch (direction)
        {
            case ORIENTATION.NORTH: return new Vector2(startPosition.x, startPosition.y + 1);
            case ORIENTATION.EAST: return new Vector2(startPosition.x + 1, startPosition.y);
            case ORIENTATION.SOUTH: return new Vector2(startPosition.x, startPosition.y - 1);
            case ORIENTATION.WEST: return new Vector2(startPosition.x - 1, startPosition.y);
            default: return MoveIntoDirection(RandomizeDirection(direction, new List<ORIENTATION>()), startPosition);
        }
    }

    private Tuple<bool, int> IsCellAvailable(List<Vector2> exploredPositions, Vector2 position, int depthRemaining)
    {
        // Check if the given position is possible
        if (nodes.Any(x => x.position == position) || exploredPositions.Any(x => x == position))
            return new Tuple<bool, int>(false, depthRemaining + 1);

        if (depthRemaining == 0)
            return new Tuple<bool, int>(true, depthRemaining);

        // Deep copy since we donc want to alter the other recursions
        // Happens after position check to avoid making a copy for nothing
        exploredPositions = ((Vector2[])exploredPositions.ToArray().Clone()).ToList();

        // Add explored item to our list after copy
        exploredPositions.Add(position);

        int smallestRemainingDepth = int.MaxValue;
        Tuple<bool, int> result;
        Vector2 newPos;

        newPos = new Vector2(position.x + 1, position.y);
        result = IsCellAvailable(exploredPositions, newPos, depthRemaining - 1);
        if (result.Item2 < smallestRemainingDepth) smallestRemainingDepth = result.Item2;
        if (result.Item1) return new Tuple<bool, int>(true, smallestRemainingDepth);

        newPos = new Vector2(position.x, position.y + 1);
        result = IsCellAvailable(exploredPositions, newPos, depthRemaining - 1);
        if (result.Item2 < smallestRemainingDepth) smallestRemainingDepth = result.Item2;
        if (result.Item1) return new Tuple<bool, int>(true, smallestRemainingDepth);

        newPos = new Vector2(position.x - 1, position.y);
        result = IsCellAvailable(exploredPositions, newPos, depthRemaining - 1);
        if (result.Item2 < smallestRemainingDepth) smallestRemainingDepth = result.Item2;
        if (result.Item1) return new Tuple<bool, int>(true, smallestRemainingDepth);

        newPos = new Vector2(position.x, position.y - 1);
        result = IsCellAvailable(exploredPositions, newPos, depthRemaining - 1);
        if (result.Item2 < smallestRemainingDepth) smallestRemainingDepth = result.Item2;
        if (result.Item1) return new Tuple<bool, int>(true, smallestRemainingDepth);

        // No possible paths found
        return new Tuple<bool, int>(false, smallestRemainingDepth);
    }

    // Gets the deepest node of a specific path starting at a specific point (call with a 0 depth param)
    private Tuple<Node, int> GetDeepestNode(Node currentNode, List<Node> exploredNodes, int pathID, int depth)
    {
        exploredNodes.Add(currentNode);

        Tuple<Node, int> deepestNodeDepth = new Tuple<Node, int>(currentNode, depth);

        foreach (Node node in currentNode.GetConnectedNodes_SpecificPath(pathID))
        {
            if (!exploredNodes.Contains(node))
            {
                Tuple<Node, int> tempDeepestNode = GetDeepestNode(node, ((Node[])exploredNodes.ToArray().Clone()).ToList(), pathID, depth + 1);

                if (tempDeepestNode.Item2 > deepestNodeDepth.Item2)
                {
                    deepestNodeDepth = tempDeepestNode;
                }
            }
        }

        return deepestNodeDepth;
    }

    #endregion
}
