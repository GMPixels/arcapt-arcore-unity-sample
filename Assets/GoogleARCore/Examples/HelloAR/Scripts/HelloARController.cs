//-----------------------------------------------------------------------
// <copyright file="HelloARController.cs" company="Google">
//
// Copyright 2017 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// </copyright>
//-----------------------------------------------------------------------

namespace GoogleARCore.Examples.HelloAR
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Assets.ARCapt;
    using GoogleARCore;
    using GoogleARCore.Examples.Common;
    using UnityEngine;
    using UnityEngine.UI;
    using UnityGLTF;
    using UnityGLTF.Loader;

#if UNITY_EDITOR
    // Set up touch input propagation while using Instant Preview in the editor.
    using Input = InstantPreviewInput;
#endif

    /// <summary>
    /// Controls the HelloAR example.
    /// </summary>
    public class HelloARController : MonoBehaviour
    {
        // Start ARCapt fields
        private GameObject GroupModel3D;
        private TrackableHit Hit;
        private CloudLoader ARCaptLoader;
        private float oldTouchPosition;
        private Bounds bounds;
        private Dropdown DropdownCollections;
        private Dropdown DropdownModels;
        private Text TextMessages;
        private bool startedDownloading;
        private bool isModelLoading = false;
        private ModelMetadata CurrentModelMetadata;
        // End Arcapt fields

        /// <summary>
        /// The first-person camera being used to render the passthrough camera image (i.e. AR background).
        /// </summary>
        public Camera FirstPersonCamera;

        /// <summary>
        /// A prefab for tracking and visualizing detected planes.
        /// </summary>
        public GameObject DetectedPlanePrefab;

        /// <summary>
        /// A model to place when a raycast from a user touch hits a plane.
        /// </summary>
        public GameObject AndyPlanePrefab;

        /// <summary>
        /// A model to place when a raycast from a user touch hits a feature point.
        /// </summary>
        public GameObject AndyPointPrefab;

        /// <summary>
        /// A gameobject parenting UI for displaying the "searching for planes" snackbar.
        /// </summary>
        public GameObject SearchingForPlaneUI;

        /// <summary>
        /// The rotation in degrees need to apply to model when the Andy model is placed.
        /// </summary>
        private const float k_ModelRotation = 180.0f;

        /// <summary>
        /// A list to hold all planes ARCore is tracking in the current frame. This object is used across
        /// the application to avoid per-frame allocations.
        /// </summary>
        private List<DetectedPlane> m_AllPlanes = new List<DetectedPlane>();

        /// <summary>
        /// True if the app is in the process of quitting due to an ARCore connection error, otherwise false.
        /// </summary>
        private bool m_IsQuitting = false;

        /// <summary>
        /// The Unity Start() method
        /// </summary>
        void Start()
        {
            this.ARCaptLoader = new CloudLoader("ARCAPT_USERNAME", "ARCAPT_API_KEY");
            StartCoroutine(this.ARCaptLoader.LoadCollections());

            this.TextMessages = GameObject.Find("TextMessages").GetComponent<Text>();
            this.DropdownCollections = GameObject.Find("DropdownCollections").GetComponent<Dropdown>();
            this.DropdownModels = GameObject.Find("DropdownModels").GetComponent<Dropdown>();

            this.DropdownCollections.onValueChanged.AddListener(delegate
            {
                DropdownCollectionValueChanged(this.DropdownCollections);
            });

            this.DropdownModels.onValueChanged.AddListener(delegate
            {
                DropdownModelValueChanged(this.DropdownModels);
            });
        }

        /// <summary>
        /// The Unity Update() method.
        /// </summary>
        public void Update()
        {
            _UpdateApplicationLifecycle();

            if (startedDownloading && this.ARCaptLoader.WWWLoader != null && this.ARCaptLoader.WWWLoader.isDone)
            {
                startedDownloading = !this.ARCaptLoader.WWWLoader.isDone;
                if (this.ARCaptLoader.WWWLoader.progress < 1)
                {
                    this.ARCaptLoader.StatusMsg = "Downloading 3D model.. " + (int)(this.ARCaptLoader.WWWLoader.progress * 100) + "%";
                }
            }

            if (this.TextMessages.text != this.ARCaptLoader.StatusMsg)
            {
                this.TextMessages.text = this.ARCaptLoader.StatusMsg;
            }

            // Hide snackbar when currently tracking at least one plane.
            Session.GetTrackables<DetectedPlane>(m_AllPlanes);
            bool showSearchingUI = true;
            for (int i = 0; i < m_AllPlanes.Count; i++)
            {
                if (m_AllPlanes[i].TrackingState == TrackingState.Tracking)
                {
                    showSearchingUI = false;
                    break;
                }
            }

            SearchingForPlaneUI.SetActive(showSearchingUI);

            if (GroupModel3D != null)
            {
                if (Input.touchCount == 1)
                {
                    if (Input.GetTouch(0).phase == TouchPhase.Began)
                    {
                        oldTouchPosition = Input.GetTouch(0).position.x;
                    }
                    else if (Input.GetTouch(0).phase == TouchPhase.Moved)
                    {
                        if (Math.Abs(oldTouchPosition - Input.GetTouch(0).position.x) < 50)
                        {
                            return;
                        }
                        if (oldTouchPosition > Input.GetTouch(0).position.x)
                        {
                            GroupModel3D.transform.Rotate(0, 5f, 0, Space.Self);
                        }
                        else
                        {
                            GroupModel3D.transform.Rotate(0, -5f, 0, Space.Self);
                        }

                        oldTouchPosition = Input.GetTouch(0).position.x;
                    }

                }
                if (Input.touchCount == 2)
                {
                    this.RotateModel();
                }
            }
            else
            {
                // If the player has not touched the screen, we are done with this update.
                Touch touch;
                if (Input.touchCount < 1 || (touch = Input.GetTouch(0)).phase != TouchPhase.Began)
                {
                    return;
                }

                // If the mode is still loading, we are done with this update again.
                if (this.isModelLoading)
                {
                    return;
                }

                // Raycast against the location the player touched to search for planes.
                TrackableHit hit;
                TrackableHitFlags raycastFilter = TrackableHitFlags.PlaneWithinPolygon |
                    TrackableHitFlags.FeaturePointWithSurfaceNormal;

                if (Frame.Raycast(touch.position.x, touch.position.y, raycastFilter, out hit))
                {
                    // Use hit pose and camera pose to check if hittest is from the
                    // back of the plane, if it is, no need to create the anchor.
                    if ((hit.Trackable is DetectedPlane) &&
                        Vector3.Dot(FirstPersonCamera.transform.position - hit.Pose.position,
                            hit.Pose.rotation * Vector3.up) < 0)
                    {
                        Debug.Log("Hit at back of the current DetectedPlane");
                    }
                    else
                    {
                        this.Hit = hit;
                        if (string.IsNullOrEmpty(this.ARCaptLoader.ModelPath))
                        {
                            return;
                        }

                        this.isModelLoading = true;
                        this.ARCaptLoader.StatusMsg = "Loading 3D model. Please wait...";
                        var loader = new FileLoader(Path.GetDirectoryName(this.ARCaptLoader.ModelPath));
                        var sceneImporter = new GLTFSceneImporter(this.ARCaptLoader.ModelPath, loader);
                        StartCoroutine(sceneImporter.LoadScene(0, true, OnImportedModel));
                    }
                }
            }
        }

        /// <summary>
        /// Check and update the application lifecycle.
        /// </summary>
        private void _UpdateApplicationLifecycle()
        {
            // Exit the app when the 'back' button is pressed.
            if (Input.GetKey(KeyCode.Escape))
            {
                Application.Quit();
            }

            // Only allow the screen to sleep when not tracking.
            if (Session.Status != SessionStatus.Tracking)
            {
                const int lostTrackingSleepTimeout = 15;
                Screen.sleepTimeout = lostTrackingSleepTimeout;
            }
            else
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            if (m_IsQuitting)
            {
                return;
            }

            // Quit if ARCore was unable to connect and give Unity some time for the toast to appear.
            if (Session.Status == SessionStatus.ErrorPermissionNotGranted)
            {
                _ShowAndroidToastMessage("Camera permission is needed to run this application.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
            else if (Session.Status.IsError())
            {
                _ShowAndroidToastMessage("ARCore encountered a problem connecting.  Please start the app again.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
        }

        /// <summary>
        /// Actually quit the application.
        /// </summary>
        private void _DoQuit()
        {
            Application.Quit();
        }

        /// <summary>
        /// Show an Android toast message.
        /// </summary>
        /// <param name="message">Message string to show in the toast.</param>
        private void _ShowAndroidToastMessage(string message)
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (unityActivity != null)
            {
                AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
                unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    AndroidJavaObject toastObject = toastClass.CallStatic<AndroidJavaObject>("makeText", unityActivity,
                        message, 0);
                    toastObject.Call("show");
                }));
            }
        }

        // Start ARCapt methods
        private void RotateModel()
        {
            // Store both touches.
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            // Find the position in the previous frame of each touch.
            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

            // Find the magnitude of the vector (the distance) between the touches in each frame.
            float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
            float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

            // Find the difference in the distances between each frame.
            float deltaMagnitudeDiff = (prevTouchDeltaMag - touchDeltaMag) * GroupModel3D.transform.localScale.x * 0.001f;

            var newScale = GroupModel3D.transform.localScale.x * ((GroupModel3D.transform.localScale.x - deltaMagnitudeDiff) / GroupModel3D.transform.localScale.x);

            GroupModel3D.transform.localScale = new Vector3(newScale, newScale, newScale);
            GroupModel3D.transform.localPosition = new Vector3(0, -bounds.min.y * GroupModel3D.transform.localScale.y, 0);
        }

        private void OnImportedModel(GameObject gameObject)
        {
            this.GroupModel3D = gameObject;
            var bounds = new Bounds(transform.position, Vector3.one);
            Renderer[] renderers = GroupModel3D.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            GroupModel3D.transform.SetPositionAndRotation(Hit.Pose.position, Hit.Pose.rotation);
            GroupModel3D.transform.localPosition = new Vector3(0, -bounds.min.y * GroupModel3D.transform.localScale.y, 0);

            // Create an anchor to allow ARCore to track the hitpoint as understanding of the physical
            // world evolves.
            if (Hit.Trackable != null)
            {
                var anchor = Hit.Trackable.CreateAnchor(Hit.Pose);
                GroupModel3D.transform.parent = anchor.transform;

                GroupModel3D.transform.localScale = new Vector3((float)this.CurrentModelMetadata.ScaX, (float)this.CurrentModelMetadata.ScaY, (float)this.CurrentModelMetadata.ScaZ);
                GroupModel3D.transform.Rotate((float)this.CurrentModelMetadata.RotX, (float)this.CurrentModelMetadata.RotY, (float)this.CurrentModelMetadata.RotZ);
                GroupModel3D.transform.position.Set((float)this.CurrentModelMetadata.PosX, (float)this.CurrentModelMetadata.PosY, (float)this.CurrentModelMetadata.PosZ);
            }

            var animation = GroupModel3D.GetComponent<Animation>();
            if (animation != null)
            {
                animation.Play();
            }

            this.isModelLoading = false;
            this.ARCaptLoader.StatusMsg = "Slide left/right to rotate. Use pinch to zoom in/out.";
        }

        private void RemoveModel()
        {
            if (this.GroupModel3D)
            {
                Destroy(GroupModel3D);
                GroupModel3D = null;
            }
        }

        private void DropdownCollectionValueChanged(Dropdown change)
        {
            if (change.captionText.text == "Collections")
            {
                return;
            }
            StartCoroutine(this.ARCaptLoader.LoadModels(this.ARCaptLoader.Collections[change.value - 1].id));
        }

        private void DropdownModelValueChanged(Dropdown change)
        {
            if (change.captionText.text == "Models")
            {
                return;
            }

            if (this.ARCaptLoader.CurrentModels != null && !startedDownloading)
            {
                this.RemoveModel();
                startedDownloading = true;
                var model = this.ARCaptLoader.CurrentModels[change.value - 1];
                StartCoroutine(this.ARCaptLoader.LoadModel(model.id));
                this.CurrentModelMetadata = model.metadata;
            }
        }
        // End ARCapt methods
    }
}
