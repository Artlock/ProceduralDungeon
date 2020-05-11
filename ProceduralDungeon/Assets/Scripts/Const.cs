using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utils {	

    public enum ORIENTATION
    {
        NONE = 0,
        NORTH = (1 << 0),
        EAST = (1 << 1),
        SOUTH = (1 << 2),
        WEST = (1 << 3),
    }

    // Transform an ORIENTATION into angle
    public static float OrientationToAngle(ORIENTATION orientation, ORIENTATION origin = ORIENTATION.NORTH)
    {
        float toNorthAngle = 0;
        switch (orientation) {
            case ORIENTATION.NORTH: toNorthAngle = 0.0f; break;
            case ORIENTATION.EAST: toNorthAngle = 90.0f; break;
            case ORIENTATION.SOUTH: toNorthAngle = 180.0f; break;
            case ORIENTATION.WEST: toNorthAngle = 270.0f; break;
            default: toNorthAngle = 270.0f; break;
        }
        if (origin == ORIENTATION.NORTH) {
            return toNorthAngle;
        }
        float originToNorthAngle = OrientationToAngle(origin, ORIENTATION.NORTH);
        return Mathf.Repeat(toNorthAngle - originToNorthAngle, 360.0f);
    }

    // Transform an angle into ORIENTATION
    public static ORIENTATION AngleToOrientation(float angle, ORIENTATION origin = ORIENTATION.NORTH)
    {
        int roundAngle = (int)Mathf.Round(angle / 90.0f);
        switch (origin)
        {
            case ORIENTATION.NORTH: roundAngle += 0; break;
            case ORIENTATION.EAST: roundAngle += 1; break;
            case ORIENTATION.SOUTH: roundAngle += 2; break;
            case ORIENTATION.WEST: roundAngle += 3; break;
        }
        roundAngle = roundAngle % 4;
        if(roundAngle < 0)
        {
            roundAngle += 4;
        }
        switch(roundAngle)
        {
            case 0: return ORIENTATION.NORTH;
            case 1: return ORIENTATION.EAST;
            case 2: return ORIENTATION.SOUTH;
            case 3: return ORIENTATION.WEST;
            default: return ORIENTATION.NONE;
        }
    }

	// Transform an ORIENTATION into direction
	public static Vector2Int OrientationToDir(ORIENTATION orientation)
	{
		switch (orientation)
		{
			case ORIENTATION.NORTH: return new Vector2Int(0,1);
			case ORIENTATION.EAST: return new Vector2Int(1, 0);
			case ORIENTATION.SOUTH: return new Vector2Int(0, -1);
			case ORIENTATION.WEST: return new Vector2Int(-1, 0);
			default: return new Vector2Int(0, 0);
		}
	}

	public static ORIENTATION OppositeOrientation(ORIENTATION orientation)
	{
		switch (orientation)
		{
			case ORIENTATION.NORTH: return ORIENTATION.SOUTH;
			case ORIENTATION.EAST: return ORIENTATION.WEST;
			case ORIENTATION.SOUTH: return ORIENTATION.NORTH;
			case ORIENTATION.WEST: return ORIENTATION.EAST;
			default: return ORIENTATION.NONE;
		}
	}

	// Transforms an angle into a discrete angle.
	public static float DiscreteAngle(float angle, float step)
    {
        return Mathf.Round(angle / step) * step;
    }

}
