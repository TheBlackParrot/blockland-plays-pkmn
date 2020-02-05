datablock ParticleData(CameraParticleA)
{
	dragCoefficient      = 0;
	gravityCoefficient   = -0.75;
	inheritedVelFactor   = 0;
	constantAcceleration = 0.0;
	lifetimeMS           = 0;
	lifetimeVarianceMS   = 0;
	textureName          = "base/data/particles/thinring";
	spinSpeed		= 0.0;
	spinRandomMin		= 0.0;
	spinRandomMax		= 0.0;
	colors[0]     = "0 0 0 0.0";
	colors[1]     = "0 0 0 0.0";
	colors[2]     = "0 0 0 0.0";
	sizes[0]      = 0;
	sizes[1]      = 0;
	sizes[2]      = 0.0;
	times[0]      = 0;
	times[1]      = 0.2;
	times[2]      = 1;

	useInvAlpha = false;
};
datablock ParticleEmitterData(CameraEmitterA)
{
   ejectionPeriodMS = 1000;
   periodVarianceMS = 0;
   ejectionVelocity = 0.0;
   ejectionOffset   = 0.40;
   velocityVariance = 0.0;
   thetaMin         = 0;
   thetaMax         = 180;
   phiReferenceVel  = 0;
   phiVariance      = 360;
   overrideAdvance = false;
   particles = "CameraParticleA";
   
   useEmitterColors = true;
};

datablock ParticleData(playerTeleportParticleB)
{
	dragCoefficient      = 0;
	gravityCoefficient   = -0.25;
	inheritedVelFactor   = 0;
	constantAcceleration = 0.0;
	lifetimeMS           = 0;
	lifetimeVarianceMS   = 0;
	textureName          = "";
	spinSpeed		= 0.0;
	spinRandomMin		= 0.5;
	spinRandomMax		= 1.0;
	colors[0]     = "0 0 0 0";
	colors[1]     = "0 0 0 0";
	colors[2]     = "0 0 0 0";
	sizes[0]      = 0;
	sizes[1]      = 0.0;
	sizes[2]      = 0.0;
	times[0]      = 0;
	times[1]      = 0.5;
	times[2]      = 1;

	useInvAlpha = false;
};
datablock ParticleEmitterData(playerTeleportEmitterB)
{
   ejectionPeriodMS = 1000;
   periodVarianceMS = 0;
   ejectionVelocity = 0;
   velocityVariance = 0;
   ejectionOffset   = 0.5;
   thetaMin         = 1.5;
   thetaMax         = 90;
   phiReferenceVel  = 0;
   phiVariance      = 360;
   overrideAdvance = false;
   particles = "playerTeleportEmitterB";
   
   useEmitterColors = true;
};

datablock ParticleData(playerTeleportParticleA)
{
	dragCoefficient      = 3;
	gravityCoefficient   = -0.0;
	inheritedVelFactor   = 1;
	constantAcceleration = 0.1;
	lifetimeMS           = 0;
	lifetimeVarianceMS   = 0;
	textureName          = "base/data/particles/thinring";
	spinSpeed		= 0.1;
	spinRandomMin		= 0.05;
	spinRandomMax		= 0.15;
	colors[0]     = "0 0 0 0";
	colors[1]     = "0 0 0 0";
	colors[2]     = "0 0 0 0";
	sizes[0]      = 0;
	sizes[1]      = 0;
	sizes[2]      = 0;
	times[0]      = 0;
	times[1]      = 1.5;
	times[2]      = 1;

	useInvAlpha = false;
};
datablock ParticleEmitterData(playerTeleportEmitterA)
{
   ejectionPeriodMS = 1000.0;
   periodVarianceMS = 0.0;
   ejectionVelocity = 1.5;
   velocityVariance = 1;
   ejectionOffset   = 0.1;
   thetaMin         = 1.5;
   thetaMax         = 90;
   phiReferenceVel  = 0;
   phiVariance      = 360;
   overrideAdvance = false;
   particles = "playerTeleportEmitterA";
   
   useEmitterColors = true;
};