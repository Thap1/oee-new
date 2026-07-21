namespace OeeNew.Domain.Production;

/// <summary>Fixed status vocabulary for ingested machine readings (Architecture Spine AD-3) — never a free string.</summary>
public enum MachineStatus
{
    Running,
    Stopped,
    Idle,
    Fault,
}
