using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

internal static class ForceApproveUnityMcpConnections
{
    private const BindingFlags StaticFlags =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [MenuItem("Tools/MCP/Force Approve Active Connections")]
    private static void ForceApproveActiveConnections()
    {
        try
        {
            var transportStoreType = FindType("Unity.AI.MCP.Editor.TransportStore");
            if (transportStoreType == null)
                throw new InvalidOperationException("Unity MCP TransportStore type was not found.");

            var getStatesMethod = transportStoreType.GetMethod(
                "GetActiveTransportStates",
                StaticFlags);
            if (getStatesMethod == null)
                throw new MissingMethodException(
                    transportStoreType.FullName,
                    "GetActiveTransportStates");

            if (getStatesMethod.Invoke(null, null) is not IEnumerable states)
                throw new InvalidOperationException("Unity MCP returned no transport state collection.");

            var approvedCount = 0;
            foreach (var state in states)
            {
                if (state == null)
                    continue;

                var approvalField = state.GetType().GetField("ApprovalState", InstanceFlags);
                if (approvalField == null)
                    throw new MissingFieldException(state.GetType().FullName, "ApprovalState");

                var approvedValue = Enum.Parse(approvalField.FieldType, "Approved");
                approvalField.SetValue(state, approvedValue);
                approvedCount++;
            }

            Debug.Log($"[Unity MCP] Forced {approvedCount} active connection(s) to Approved.");
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static Type FindType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(fullName, false);
            if (type != null)
                return type;
        }

        return null;
    }
}
