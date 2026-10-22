using UnityEngine;

namespace KinematicCharacterController.Examples
{
    public partial class ExampleCharacterController
    {
        private bool _interactRequestedL = false;
        private bool _interactRequestedR = false;

        private void HandleItemPickup(ItemPickup pickup)
        {
            ItemObject item = pickup.ItemData;

            switch (item.type)
            {
                case ItemType.Equipment:
                    EquipmentObject equipment = item as EquipmentObject;
                    if (equipment != null)
                    {
                        // add item to inventory here

                        Debug.Log($"Picked up Equipment: {item.name}");
                    }
                    break;
                default:
                    break;
            }

            pickup.OnPickedUp();
        }

        void Interact(bool isRight = false)
        {
            Hand activeHand = isRight ? rightHand : leftHand;

            PlayerContext context = new PlayerContext(transform, playerCamera, Motor);

            if (!activeHand.IsEmpty)
            {
                activeHand.Use(context);

                // Reset the interact request right after use
                if (!isRight)
                {
                    _interactRequestedL = false;
                    return;
                }
                else
                {
                    _interactRequestedR = false;
                    return;
                }
            }

            Vector3 origin = playerCamera.transform.position;
            Vector3 direction = playerCamera.transform.forward;

            RaycastHit hit = isRight ? rightHandHit : leftHandHit;

            if (Physics.SphereCast(origin, grabRadius, direction, out hit, grabDistance, interactableLayer))
            {

                // Update the appropriate hand hit variable
                if (isRight)
                {
                    rightHandHit = hit;
                }
                else
                {
                    leftHandHit = hit;
                }

                IInteractable interactable = hit.collider.GetComponent<IInteractable>();
                if (interactable != null && interactable.CanInteract())
                {
                    interactable.OnInteract(context);
                    ResetInteractRequest(isRight);
                    return;
                }

                ItemPickup pickup = hit.collider.GetComponent<ItemPickup>();
                if (pickup != null)
                {
                    Debug.Log("picking up item" + pickup.ItemData.name);
                    activeHand.PickUp(pickup);
                    // HandleItemPickup(pickup);
                    ResetInteractRequest(isRight);
                    return;
                }

                // Next, check if it's a package
                PackagePickup packagePickup = hit.collider.GetComponent<PackagePickup>();
                if (packagePickup != null)
                {
                    HandlePackagePickup(packagePickup);
                    ResetInteractRequest(isRight);
                    return;
                }

                // THIS SUCKS - change later
                if (hit.collider.name.Contains("PackageDropoff"))
                {
                    DropPackage();
                    ResetInteractRequest(isRight);
                    return;
                }
            }



            // We interacted so stop the request
            ResetInteractRequest(isRight);

        }

        void ResetInteractRequest(bool isRight)
        {
            if (isRight)
            {
                _interactRequestedR = false;
            }
            else
            {
                _interactRequestedL = false;
            }
        }

        void HandlePackagePickup(PackagePickup packagePickup)
        {
            if (playerCharacter.currentPackage != null)
            {
                Debug.Log("Already carrying a package!");
                return;
            }
            // Apply package modifiers
            playerCharacter.AcceptPackage(packagePickup.PackageData);

            //Transform pickup
            Transform dropoff = LocationRegistry.ResolveDropoff(packagePickup.PackageData.dropoffLocation);
            deliveryWaypoint.target = dropoff.position;

            // Update world sections if needed
            WorldSectionManager.Instance.RefreshWorld(packagePickup.PackageData);

            Debug.Log($"Picked up package: {packagePickup.PackageData.themeName}");
        }

        public void DropPackage()
        {
            if (playerCharacter.currentPackage == null) return;

            // Remove all modifiers applied by the package
            playerCharacter.modifierHandler.RemoveAll();

            // Optionally reset world sections
            WorldSectionManager.Instance.RefreshWorld(defaultMemoryType);

            Transform pickup = LocationRegistry.FindNearestPickup(playerCamera.transform);
            deliveryWaypoint.target = pickup.position;

            playerCharacter.currentPackage = null;
        }

    }
}

