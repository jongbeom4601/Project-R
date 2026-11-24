using System;
using UnityEngine;
using UnityEngine.Serialization;
using Milestone = ProjectR.StageTrigger.Milestone;

namespace ProjectR
{
    public sealed class HorrorEventManager : MonoBehaviour
    {
        public enum StagePhase { Ready, Prepared, Active, Completed }

        [SerializeField] private CharacterController playerBody;
        [SerializeField] private DoorController corridorDoor;
        [SerializeField, FormerlySerializedAs("lockedFoyerDoor"),
         Tooltip("Locked door in the last apartment. Trying it after the bedroom-2 door has slammed unlocks the corridor door.")]
        private DoorController unlockingDoor;
        [SerializeField] private BedroomDoorScare bedroomDoorScare;
        [SerializeField] private SpatialManager space;
        [SerializeField] private TransitionController transition;

        private bool triedUnlockingDoor;

        public CharacterController PlayerBody => playerBody;
        public int CurrentStage { get; private set; }
        public StagePhase Phase { get; private set; }
        public bool HasEscaped => CurrentStage == 6 && Phase == StagePhase.Completed;
        public event Action<int> StageCompleted;
        public event Action<int> StageActivated;
        public event Action ClimaxRequested;

        internal bool IsWaitingForUnlockingDoor(DoorController door) =>
            door == corridorDoor && unlockingDoor != null && !triedUnlockingDoor;

        public bool CanOpen(int unlockStage)
        {
            if (unlockStage != 2)
                return CurrentStage >= unlockStage;

            // Stage-02 doors wait for the corridor change; with no corridor change in the scene they are never held.
            return CurrentStage < 2 || space == null || space.IsExtended;
        }

        public void OnLockedDoorInteracted(DoorController door)
        {
            if (door == unlockingDoor)
            {
                if (bedroomDoorScare == null || bedroomDoorScare.HasSlammed)
                    triedUnlockingDoor = true;
                return;
            }

            if (door != corridorDoor || !door.IsPassageLocked)
                return;

            // The barrier proves the player crossed, even if they turned before reaching the normal corridor marker.
            if (CurrentStage == 0 && Phase == StagePhase.Active)
                CompleteStage();

            if (CurrentStage == 1 && Phase == StagePhase.Ready)
            {
                ActivateStage();
                CompleteStage();
            }
        }

        public void BeginDoorStage(int stage)
        {
            if (CurrentStage == stage && Phase == StagePhase.Ready)
                ActivateStage();
        }

        public void FinishClimax()
        {
            if (CurrentStage == 5 && Phase == StagePhase.Prepared)
                ActivateStage();
        }

        public void Reach(Milestone milestone)
        {
            if (HasEscaped)
                return;

            switch (CurrentStage, milestone, Phase)
            {
                case (0, Milestone.LivingRoom, StagePhase.Ready):
                    ActivateStage();
                    break;
                case (5, Milestone.DarkCorridorMidpoint, StagePhase.Ready):
                    Phase = StagePhase.Prepared;
                    ClimaxRequested?.Invoke();
                    break;
                case (0, Milestone.NormalCorridor, StagePhase.Active):
                case (2, Milestone.ExtendedCorridor, StagePhase.Active):
                case (3, Milestone.StrangeRoom, StagePhase.Active):
                case (4, Milestone.RepeatExit, StagePhase.Active):
                case (5, Milestone.DarkCorridorEnd, StagePhase.Active):
                case (6, Milestone.Exterior, StagePhase.Active):
                    CompleteStage();
                    break;
            }
        }

        private void LateUpdate()
        {
            // Without a corridor change in the scene (space or transition empty), stage 02 stays prepared.
            if (Time.timeScale == 0f || CurrentStage != 2 || Phase != StagePhase.Prepared || space == null || transition == null ||
                !transition.CanChangeCorridor())
                return;

            space.ExtendCorridor();
            ActivateStage();
        }

        private void ActivateStage()
        {
            Phase = StagePhase.Active;
            StageActivated?.Invoke(CurrentStage);
        }

        private void CompleteStage()
        {
            int finished = CurrentStage;
            if (finished == 6)
            {
                Phase = StagePhase.Completed;
            }
            else
            {
                CurrentStage++;
                Phase = CurrentStage == 2 ? StagePhase.Prepared : StagePhase.Ready;
            }

            StageCompleted?.Invoke(finished);
        }
    }
}
