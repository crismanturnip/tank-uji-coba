using System;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

namespace Tatsuya;

public class Tatsuya : Bot
{
    private bool locked;
    private int lockedTargetId = -1;
    private double lockedTargetX;
    private double lockedTargetY;
    private double lockedTargetEnergy = double.MaxValue;
    private double lockedTargetDistance = double.MaxValue;
    private double lockedTargetVelocity;
    private double lockedTargetHeading;

    private int turnCounter;
    private int lastSeenTurn;

    private const int LockTimeout = 10;
    private const double CloseRangeDistance = 150.0;
    private const double EnemyRammingThreshold = 20.0;
    private const double WallMargin = 40.0;
    private const double DefaultFirePower = 1.0;
    private const double CloseFirePower = 2.0;
    private const double FinisherFirePower = 3.0;

    public static void Main(string[] args)
    {
        new Tatsuya().Start();
    }

    private Tatsuya() : base(BotInfo.FromFile("Tatsuya.json")) { }

    public override void Run()
    {
        ConfigureIdentity();

        AdjustGunForBodyTurn = true;
        AdjustRadarForBodyTurn = true;
        AdjustRadarForGunTurn = true;

        while (IsRunning)
        {
            turnCounter++;

            if (locked && turnCounter - lastSeenTurn > LockTimeout)
            {
                ClearLock();
            }

            GunTurnRate = MaxGunTurnRate;
            RadarTurnRate = MaxRadarTurnRate;

            if (locked)
            {
                ChaseLockedTarget();
                AimAndFire();
            }
            else
            {
                TurnRate = MaxTurnRate;
                SetTurnRadarRight(360);
            }

            Go();
        }
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {
        var scannedDistance = DistanceTo(e.X, e.Y);
        var enemyEnergy = e.Energy;

        if (locked && e.ScannedBotId == lockedTargetId)
        {
            UpdateLock(e, scannedDistance);
        }
        else if (!locked || enemyEnergy < lockedTargetEnergy)
        {
            locked = true;
            lockedTargetId = e.ScannedBotId;
            UpdateLock(e, scannedDistance);
        }
    }

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        TurnRate = 5;

        if (locked)
        {
            SetForward(120);
        }
    }

    public override void OnHitWall(HitWallEvent e)
    {
        SetForward(-100);
        TurnRate = MaxTurnRate;
    }

    public override void OnHitBot(HitBotEvent e)
    {
        if (locked && e.VictimId == lockedTargetId)
        {
            lockedTargetX = e.X;
            lockedTargetY = e.Y;
            lockedTargetEnergy = e.Energy;
            lockedTargetDistance = DistanceTo(e.X, e.Y);
            lastSeenTurn = turnCounter;

            SetTurnGunLeft(GunBearingTo(e.X, e.Y));

            if (GunHeat == 0 && Energy > 0.5)
            {
                SetFire(Math.Min(FinisherFirePower, Energy - 0.1));
            }

            SetForward(80);
            return;
        }

        var escapeBearing = NormalizeRelativeAngle(BearingTo(e.X, e.Y) + 180.0);
        TurnRate = Clamp(escapeBearing, -MaxTurnRate, MaxTurnRate);
        SetForward(-50);
    }

    public override void OnBotDeath(BotDeathEvent e)
    {
        if (locked && e.VictimId == lockedTargetId)
        {
            ClearLock();
        }
    }

    private void ConfigureIdentity()
    {
        BodyColor = Color.FromArgb(34, 42, 54);
        TurretColor = Color.FromArgb(90, 210, 148);
        RadarColor = Color.FromArgb(255, 202, 87);
        GunColor = Color.FromArgb(235, 238, 244);
        BulletColor = Color.FromArgb(255, 95, 91);
        ScanColor = Color.FromArgb(124, 230, 185);
        TracksColor = Color.FromArgb(21, 25, 31);
    }

    private void UpdateLock(ScannedBotEvent e, double scannedDistance)
    {
        lockedTargetX = e.X;
        lockedTargetY = e.Y;
        lockedTargetDistance = scannedDistance;
        lockedTargetEnergy = e.Energy;
        lockedTargetVelocity = e.Speed;
        lockedTargetHeading = e.Direction;
        lastSeenTurn = turnCounter;
    }

    private void ChaseLockedTarget()
    {
        var bearingToTarget = BearingTo(lockedTargetX, lockedTargetY);
        TurnRate = Clamp(bearingToTarget, -MaxTurnRate, MaxTurnRate);

        if (IsNearWall())
        {
            TurnTowardArenaCenter();
            SetForward(140);
            return;
        }

        if (lockedTargetEnergy < EnemyRammingThreshold)
        {
            SetForward(1000);
            return;
        }

        var moveDistance = lockedTargetDistance > CloseRangeDistance
            ? lockedTargetDistance - CloseRangeDistance
            : -10;

        SetForward(moveDistance);
    }

    private void AimAndFire()
    {
        PredictEnemyPosition(out var predictedX, out var predictedY);

        var gunBearing = NormalizeRelativeAngle(GunBearingTo(predictedX, predictedY));
        GunTurnRate = Clamp(gunBearing, -MaxGunTurnRate, MaxGunTurnRate);

        var radarBearing = NormalizeRelativeAngle(RadarBearingTo(lockedTargetX, lockedTargetY));
        RadarTurnRate = Clamp(radarBearing * 1.6, -MaxRadarTurnRate, MaxRadarTurnRate);

        var firePower = ChooseFirePower();

        if (GunHeat == 0 && Energy > firePower + 0.1 && Math.Abs(gunBearing) <= AimTolerance())
        {
            SetFire(firePower);
        }
    }

    private double ChooseFirePower()
    {
        var firePower = lockedTargetEnergy < EnemyRammingThreshold
            ? FinisherFirePower
            : lockedTargetDistance < CloseRangeDistance
                ? CloseFirePower
                : DefaultFirePower;

        if (Energy < 18)
        {
            firePower = Math.Min(firePower, 0.8);
        }

        return Math.Clamp(firePower, 0.2, Math.Min(FinisherFirePower, Energy - 0.1));
    }

    private double AimTolerance()
    {
        return lockedTargetDistance < CloseRangeDistance ? 8.0 : 4.0;
    }

    private void PredictEnemyPosition(out double predictedX, out double predictedY)
    {
        var firePower = ChooseFirePower();
        var bulletSpeed = CalcBulletSpeed(firePower);
        var timeToImpact = lockedTargetDistance / bulletSpeed;
        var headingRadians = DegreesToRadians(lockedTargetHeading);

        predictedX = lockedTargetX + lockedTargetVelocity * timeToImpact * Math.Cos(headingRadians);
        predictedY = lockedTargetY + lockedTargetVelocity * timeToImpact * Math.Sin(headingRadians);

        predictedX = Clamp(predictedX, WallMargin, ArenaWidth - WallMargin);
        predictedY = Clamp(predictedY, WallMargin, ArenaHeight - WallMargin);
    }

    private bool IsNearWall()
    {
        return X < WallMargin ||
               X > ArenaWidth - WallMargin ||
               Y < WallMargin ||
               Y > ArenaHeight - WallMargin;
    }

    private void TurnTowardArenaCenter()
    {
        var centerDirection = DirectionTo(ArenaWidth / 2.0, ArenaHeight / 2.0);
        TurnRate = Clamp(CalcDeltaAngle(centerDirection, Direction), -MaxTurnRate, MaxTurnRate);
    }

    private void ClearLock()
    {
        locked = false;
        lockedTargetId = -1;
        lockedTargetEnergy = double.MaxValue;
        lockedTargetDistance = double.MaxValue;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(value, max));
    }
}
