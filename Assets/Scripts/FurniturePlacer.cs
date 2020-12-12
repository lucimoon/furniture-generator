﻿using System.Collections.Generic;
using UnityEngine;

public class FurniturePlacer : MonoBehaviour
{
    [SerializeField] private List<Furniture> randomFurniture;
    [SerializeField] private List<Furniture> staticFurniture;
    [SerializeField] private List<Rug> rugs;
    [SerializeField] private List<Decoration> decorations;
    [SerializeField] private int numberOfRugs = 0;
    [SerializeField] private int xLength = 1;
    [SerializeField] private int zLength = 1;
    public bool debugMode = false;
    public GameState state;
    private List<Furniture> placedFurniture;
    private List<Decoration> placedDecorations;
    private List<Rug> placedRugs;
    private List<Vector3> furnitureCoordinates;
    private List<Vector3> rugCoordinates;
    private List<Vector3> tempCoordinates;
    private List<Furniture> tempFurniture;
    private System.Random random;

    void Start()
    {
        placedFurniture = new List<Furniture>();
        placedDecorations = new List<Decoration>();
        placedRugs = new List<Rug>();
        tempCoordinates = new List<Vector3>();
        rugCoordinates = new List<Vector3>();
        RearrangeFurniture();
    }

    public void RearrangeFurniture()
    {

        foreach (Furniture item in placedFurniture.ToArray())
        {
            if (!staticFurniture.Contains(item))
            {
                placedFurniture.Remove(item);
                Destroy(item.gameObject);
            }
        }

        random = new System.Random();
        furnitureCoordinates = BuildCoordinates();
        rugCoordinates = BuildCoordinates();
        RegisterStaticFurniture();

        foreach (FurnitureCount count in state.counts)
        {
            for (int i = 0; i < count.count; i++)
            {


                Vector3 selectedPosition;
                Vector3 position;

                Furniture furniture = SelectRandom<Furniture>(randomFurniture.FindAll((Furniture item) =>
                {
                    return item.type == count.type;
                }));

                for (int placementAttempt = 0; placementAttempt < 10; placementAttempt++)
                {
                    if (furniture.alignTo.Count == 0)
                    {
                        position = RandomPosition(furnitureCoordinates);
                    }
                    else if (furniture.alignTo.Contains(FurnitureType.Wall))
                    {
                        // find all available walls (use find all walls)
                        tempCoordinates = furnitureCoordinates.FindAll(FindEdge);
                        // TODO: guard when tempCoordinates are empty
                        position = RandomPosition(tempCoordinates);
                        furniture.rotation = GetRotationDegrees(position);
                    }
                    else if (furniture.alignTo.Contains(FurnitureType.WallCorner))
                    {
                        // find all available walls (use find all walls)
                        tempCoordinates = furnitureCoordinates.FindAll(FindCorner);
                        position = RandomPosition(tempCoordinates);
                        furniture.rotation = GetRotationDegrees(position);
                    }
                    else
                    {
                        // find piece of furniture with free spaces
                        tempFurniture = placedFurniture.FindAll(furniture.FindDependantFurniture);
                        int furnitureIndex = random.Next(tempFurniture.Count);

                        if (tempFurniture.Count == 0) continue;
                        // find spaces that are on furnitures preferred parent side
                        tempCoordinates = tempFurniture[furnitureIndex].ValidSpaces(furniture).FindAll(FindFurnitureSpaces);

                        if (tempCoordinates.Count == 0) continue;

                        position = RandomPosition(tempCoordinates);
                        furniture.SetParentRelativeRotation(position, tempFurniture[furnitureIndex]);
                    }

                    bool isEnoughRoom = IsEnoughRoom(furniture, position);

                    if (isEnoughRoom)
                    {
                        selectedPosition = position;
                        furniture.origin = furniture.RotatedBottomLeft(selectedPosition);

                        Furniture newFurniture = Instantiate(furniture, furniture.WorldOrigin(selectedPosition), Quaternion.Euler(0f, furniture.rotation, 0f));

                        AddToGrid(newFurniture);
                        break;
                    }

                    if (placementAttempt == 9) Debug.Log("No room for furniture: " + furniture);
                    continue;
                }
            }
        }

        PlaceDecorations();
        PlaceRugs();
    }

    private void PlaceDecorations()
    {
        foreach (Decoration decoration in placedDecorations)
        {
            Destroy(decoration.gameObject);
        }
        placedDecorations.Clear();

        // for each dcoreation
        foreach (Decoration decoration in decorations)
        {
            // find dependent furnitures that aren't decorated
            tempFurniture = placedFurniture.FindAll((Furniture furniture) =>
            {
                return furniture.decorationSpaces.Count > 0 && decoration.parentFurniture.Contains(furniture.type);
            });
            // select one of them
            if (tempFurniture.Count == 0)
            {
                continue;
            }

            int index = random.Next(tempFurniture.Count);
            Furniture selectedFurniture = tempFurniture[index];
            index = random.Next(selectedFurniture.decorationSpaces.Count);
            Transform decorationPosition = selectedFurniture.decorationSpaces[index];
            selectedFurniture.decorationSpaces.RemoveAt(index);
            // place instance
            placedDecorations.Add(Instantiate(decoration, decorationPosition.position, decorationPosition.transform.rotation));
        }
    }

    private void PlaceRugs()
    {
        foreach (Rug rug in placedRugs)
        {
            Destroy(rug.gameObject);
        }
        placedRugs.Clear();

        for (int i = 0; i < numberOfRugs; i++)
        {
            Rug rugPrefab = SelectRandom(rugs);

            bool rugHasParents = rugPrefab.parentFurniture.Count > 0;
            bool rugFits;
            // pick random location
            // resize
            int newRugX = (int)rugPrefab.xMax;
            int newRugZ = (int)rugPrefab.zMax;

            Vector3 rugOrigin;

            if (!rugPrefab.forceMax)
            {
                newRugX = random.Next((int)rugPrefab.xMax) + 1;
                newRugZ = random.Next((int)rugPrefab.zMax) + 1;
            }


            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (rugHasParents)
                {
                    // get list of placed parents
                    List<Furniture> possibleParents = placedFurniture.FindAll((Furniture furniture) =>
                    {
                        return rugPrefab.parentFurniture.Contains(furniture.type);
                    });

                    // pick one
                    Furniture parentFurniture = SelectRandom(possibleParents);
                    rugOrigin = parentFurniture.origin;
                    // resize

                    // align based on size & position

                    //if it fits
                    rugFits = RugFits(rugOrigin, newRugX, newRugZ);
                }
                else
                {
                    // select psitions that will fit dimensions
                    List<Vector3> possiblePositions = rugCoordinates.FindAll((Vector3 coord) =>
                    {
                        return coord.x + newRugX < xLength && coord.z + newRugZ < zLength;
                    });

                    rugOrigin = SelectRandom(possiblePositions);
                    rugFits = RugFits(rugOrigin, newRugX, newRugZ);
                }

                if (rugFits)
                {
                    // place at location
                    Rug newRug = Instantiate(rugPrefab, rugOrigin, Quaternion.identity);
                    Vector3 scale = new Vector3(newRugX, rugPrefab.transform.localScale.y, newRugZ);
                    newRug.transform.localScale = scale;
                    placedRugs.Add(newRug);

                    // Remove coords
                    RemoveRugCoords(rugOrigin, newRugX, newRugZ);
                    break;
                }
            }
        }
    }

    private void RegisterStaticFurniture()
    {
        foreach (Furniture furniture in staticFurniture)
        {
            Vector3 originalPosition = furniture.transform.position;
            Vector3 position;

            // align furniture to grid
            position = new Vector3(Mathf.Round(originalPosition.x), 0f, Mathf.Round(originalPosition.z));
            furniture.transform.position = position;

            // set rotation to support furniture internal calculations
            furniture.rotation = 90 * Mathf.Floor(furniture.transform.rotation.eulerAngles.y / 90);
            // set furniture origin
            furniture.origin = furniture.GridOrigin(position);

            AddToGrid(furniture);
        }
    }

    private List<Vector3> BuildCoordinates()
    {
        List<Vector3> coords = new List<Vector3>();

        for (float x = 0f; x < xLength; x++)
        {
            for (float z = 0f; z < zLength; z++)
            {
                coords.Add(new Vector3(x, 0f, z));
            }
        }

        return coords;
    }

    private Vector3 RandomPosition(List<Vector3> coords)
    {
        int randomIndex = random.Next(coords.Count);
        Vector3 position = coords[randomIndex];

        return position;
    }

    private float GetRotationDegrees(Vector3 position)
    {
        float rotation = 0f;
        // Wall
        if (position.x == 0) rotation = 90f;
        if (position.z == zLength - 1) rotation = 180f;
        if (position.x == xLength - 1) rotation = 270f;
        // Corners
        if (position.x == 0 && position.z == 0) rotation = 0f;
        if (position.x == 0 && position.z == zLength - 1) rotation = 90f;
        if (position.x == xLength - 1 && position.z == zLength - 1) rotation = 180f;
        if (position.x == xLength - 1 && position.z == 0) rotation = 270f;

        return rotation;
    }

    private void RemoveFurnitureCoords(Furniture furniture)
    {
        Vector3 bottomLeftCoord = furniture.origin;
        List<Vector3> reservedSpaces = furniture.ReservedSpaces();

        // remove actual furniture spaces
        for (float x = 0; x < furniture.RotatedXLength(); x++)
        {
            for (float z = 0; z < furniture.RotatedZLength(); z++)
            {
                Vector3 furnitureCoord = new Vector3(bottomLeftCoord.x + x, 0f, bottomLeftCoord.z + z);
                furnitureCoordinates.Remove(furnitureCoord);
                if (!furniture.allowRug) rugCoordinates.Remove(furnitureCoord);
            }
        }

        // remove surrounding invalid spaces
        furnitureCoordinates.RemoveAll((Vector3 coord) =>
        {
            return reservedSpaces.Contains(coord);
        });
    }

    private void RemoveRugCoords(Vector3 position, float rugX, float rugZ)
    {
        for (float x = 0; x < rugX; x++)
        {
            for (float z = 0; z < rugZ; z++)
            {
                Vector3 rugCoord = new Vector3(position.x + x, 0f, position.z + z);
                rugCoordinates.Remove(rugCoord);
            }
        }
    }

    private bool IsEnoughRoom(Furniture furniture, Vector3 origin)
    {
        Vector3 bottomLeftCoord = furniture.RotatedBottomLeft(origin);

        bool isWithinGridX = bottomLeftCoord.x + furniture.RotatedXLength() <= xLength;
        bool isWithinGridZ = bottomLeftCoord.z + furniture.RotatedZLength() <= zLength;
        bool isEnoughRoom = isWithinGridX && isWithinGridZ;

        for (float x = 0; x < furniture.RotatedXLength(); x++)
        {
            if (!isEnoughRoom) break;

            for (float z = 0; z < furniture.RotatedZLength(); z++)
            {
                Vector3 furnitureCoord = new Vector3(bottomLeftCoord.x + x, 0f, bottomLeftCoord.z + z);

                if (!furnitureCoordinates.Contains(furnitureCoord))
                {
                    isEnoughRoom = false;
                    break;
                }
            }
        }

        return isEnoughRoom;
    }

    private bool FindEdge(Vector3 coord)
    {
        if (coord.x == 0 || coord.x == xLength - 1 || coord.z == 0 || coord.z == zLength - 1)
        {
            return true;
        }

        return false;
    }

    private bool FindCorner(Vector3 coord)
    {
        bool leftCorner = coord.x == 0 && (coord.z == 0 || coord.z == zLength - 1);
        bool rightCorner = coord.x == xLength - 1 && (coord.z == 0 || coord.z == zLength - 1);

        if (leftCorner || rightCorner) return true;

        return false;
    }

    private bool FindFurnitureSpaces(Vector3 coord)
    {
        if (furnitureCoordinates.Contains(coord)) return true;

        return false;
    }

    private void AddToGrid(Furniture furniture)
    {
        RemoveFurnitureCoords(furniture);
        placedFurniture.Add(furniture);
    }

    private T SelectRandom<T>(List<T> list)
    {
        int index = random.Next(list.Count);
        return list[index];
    }

    private bool RugFits(Vector3 origin, int xLength, int zLength)
    {
        bool fits = true;
        for (float x = 0; x < xLength; x++)
        {
            if (!fits) break;

            for (float z = 0; z < zLength; z++)
            {
                Vector3 testPosition = new Vector3(origin.x + x, 0f, origin.z + z);
                fits = rugCoordinates.Contains(testPosition);
                if (!fits) break;
            }
        }
        return fits;
    }
}

