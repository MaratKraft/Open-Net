using System;
namespace NetworkHub.Dns;
// Diagnostic evidence never authorizes a write. Recovery authority remains the protected journal.
public sealed class DnsOperationEvidence
{
    public string Action {get;set;}="";
    public Guid AdapterId {get;set;}
    public string AdapterName {get;set;}="";
    public string Scope {get;set;}="selected adapter";
    public string StartedUtc {get;set;}=DateTime.UtcNow.ToString("O");
    public string BeforeFingerprint {get;set;}="";
    public string AfterFingerprint {get;set;}="";
    public int RequestedWrites {get;set;}
    public int AcceptedWrites {get;set;}
    public int VerifiedWrites {get;set;}
    public string WriteResult {get;set;}="not requested";
    public string VerificationResult {get;set;}="not performed";
    public string RecoveryResult {get;set;}="not required";
    public string CacheResult {get;set;}="not performed";
    public string HttpsResult {get;set;}="not tested";
    public bool? ValuesChanged {get;set;}
    public bool NetworkStateChangeConfirmed=>false;
    public void Begin(string action,DnsSnapshot before){Action=action;AdapterId=before.AdapterId;AdapterName=before.AdapterName;BeforeFingerprint=before.Fingerprint();}
}
