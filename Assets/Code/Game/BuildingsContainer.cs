﻿using Assets.Code.Interface;
using Assets.Code.Interface.Game;
using Assets.Code.Tools;
using Assets.Code.Tools.Prefabs;
using UnityEngine;

namespace Assets.Code.Game
{
    public class BuildingsContainer : MonoBehaviour
    {
        public static BuildingsContainer Instance { get; set; }

        public Building[,] BuildingsGrid { get; set; }


        public readonly string[] PrefabsNames =
        {
            "Plain",
            "Rock",
            "Water",
            "Forest",
            "House - wood, 1",
            "House - wood, 2",
        };


        private void Start()
        {
            if (Instance != null)
            {
                Debug.LogError("BuildingsContainer.Instance is already set");
                return;
            }

            Instance = this;
        }

        public void CreateBuilding(short id, Vector2 position)
        {
            var building 
                = BuildingPrefabsContainer.Instance.Get(PrefabsNames[id]).Instantiate();

            building.Instance.transform.SetParent(
                Ui.GameBuildingsContainer.Transform, true);

            building.Holder.transform.SetParent(
                Ui.GameBuildingsContainer.Transform, true);

            building.BuildingIsometricController.IsometricPosition = position;
            building.HolderIsometricController.IsometricPosition = position;

            building.BuildingController.Building = building;
            building.HolderController.Building = building;

            Instance.BuildingsGrid[(int)position.x, (int)position.y]
                = building;
        }
    }
}
