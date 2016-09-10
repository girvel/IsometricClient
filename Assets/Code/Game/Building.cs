﻿using Assets.Code.Interface;
using UnityEngine;

namespace Assets.Code.Game
{
    public class Building
    {
        public GameObject Instance { get; set; }

        public BuildingController BuildingController { get; set; }

        public IsometricController BuildingIsometricController { get; set; }

        public SpriteRenderer BuildingSpriteRenderer { get; set; }



        public GameObject Holder { get; set; }

        public HolderController HolderController { get; set; }

        public IsometricController HolderIsometricController { get; set; }

        public SpriteRenderer HolderSpriteRenderer { get; set; }



        public Building(GameObject instance, GameObject holder)
        {
            Instance = instance;
            Holder = holder;

            BuildingController = Instance.GetComponent<BuildingController>();
            BuildingIsometricController = Instance.GetComponent<IsometricController>();
            BuildingSpriteRenderer = Instance.GetComponent<SpriteRenderer>();

            HolderController = Holder.GetComponent<HolderController>();
            HolderIsometricController = Holder.GetComponent<IsometricController>();
            HolderSpriteRenderer = Holder.GetComponent<SpriteRenderer>();
        }
    }
}