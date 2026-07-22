// PSX-style vertex snapping using camera orientation with resolution scaling
void PSXVertexJitter_float(
    float3 PositionWS,
    float3 CameraPosition,
    float3 CameraForward,
    float3 CameraRight,
    float3 CameraUp,
    float NearDistance,
    float FarDistance,
    float SnapResolution,
    float JitterIntensity,
    float ResolutionScaleStart,
    float ResolutionScaleAmount,
    out float3 JitteredPosition)
{
    // Calculate distance from camera/player
    float distance = length(PositionWS - CameraPosition);
    
    // Calculate jitter factor (0 at near, 1 at far)
    // Near = high quality/resolution, Far = low quality/more jitter
    float jitterFactor = saturate((distance - NearDistance) / (FarDistance - NearDistance));
    
    // NEW: Calculate resolution scaling based on distance threshold
    // If distance > ResolutionScaleStart, reduce snap resolution
    float resolutionReduction = 0.0;
    if (distance > ResolutionScaleStart)
    {
        // Calculate how far past the threshold we are
        float distancePastThreshold = distance - ResolutionScaleStart;
        // Scale down resolution, capped at ResolutionScaleAmount
        resolutionReduction = min(distancePastThreshold, ResolutionScaleAmount);
    }
    
    // Apply resolution reduction (lower resolution = more snapping/jitter)
    float adjustedSnapResolution = max(SnapResolution - resolutionReduction, 1.0);
    
    // Calculate effective resolution based on distance
    // Closer objects = higher resolution, farther = lower resolution (more snapping)
    float distanceScale = lerp(1.0, JitterIntensity, jitterFactor);
    float effectiveResolution = adjustedSnapResolution / max(distanceScale, 0.1);
    
    // Transform vertex to camera-aligned space (like screen space but in world units)
    // This makes snapping respond to camera rotation
    float3 relativePos = PositionWS - CameraPosition;
    
    // Project onto camera's right/up plane (screen-aligned in world space)
    float2 screenAlignedPos;
    screenAlignedPos.x = dot(relativePos, CameraRight);
    screenAlignedPos.y = dot(relativePos, CameraUp);
    float depthFromCamera = dot(relativePos, CameraForward);
    
    // Snap to grid in camera-aligned space
    // This is where the PSX "vertex jitter" comes from
    screenAlignedPos = floor(screenAlignedPos * effectiveResolution) / effectiveResolution;
    
    // Reconstruct world position from snapped camera-space position
    JitteredPosition = CameraPosition +
                      CameraRight * screenAlignedPos.x +
                      CameraUp * screenAlignedPos.y +
                      CameraForward * depthFromCamera;
}

// Wrapper for Shader Graph
void Vertex_Jitter_float(
    float3 PositionWS,
    float3 CameraPosition,
    float3 CameraForward,
    float3 CameraRight,
    float3 CameraUp,
    float NearDistance,
    float FarDistance,
    float SnapResolution,
    float JitterIntensity,
    float ResolutionScaleStart,
    float ResolutionScaleAmount,
    out float3 JitteredPosition)
{
    PSXVertexJitter_float(PositionWS, CameraPosition, CameraForward, CameraRight, CameraUp, NearDistance, FarDistance, SnapResolution, JitterIntensity, ResolutionScaleStart, ResolutionScaleAmount, JitteredPosition);
}