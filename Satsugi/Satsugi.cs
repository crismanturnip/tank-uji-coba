using System;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

namespace Satsugi;

public class Satsugi : Bot
{
    private const double WallMargin = 70;
    private const double RamDistance = 135;
    private const double CloseFireDistance = 230;
    private const double ChaseDistance = 520;
    private const double RadarLockOvershoot = 2.0;
    private const double PredictionWallMargin = 20;
    private const int LostTargetTurns = 10;

    private EnemySnapshot target;
    private int ramDirection = 1;
    private int shotsFired;
    private int shotsHit;
    private int wallMisses;

    public static void Main(string[] args)
    {
        new Satsugi().Start();
    }

    private Satsugi() : base(BotInfo.FromFile("Satsugi.json")) { }

    public override void Run()
    {
        ConfigureIdentity();

        AdjustGunForBodyTurn = true;
        AdjustRadarForBodyTurn = true;
        AdjustRadarForGunTurn = true;

        MaxSpeed = 8;
        GunTurnRate = MaxGunTurnRate;
        RadarTurnRate = MaxRadarTurnRate;

        while (IsRunning)
        {
            ForgetLostTarget();
            ControlRadar();
            MoveAggressively();
            AimAndFire();
            Go();
        }
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {
        var previous = target != null && target.Id == e.ScannedBotId ? target : null;
        target = new EnemySnapshot
        {
            Id = e.ScannedBotId,
            X = e.X,
            Y = e.Y,
            PreviousX = previous?.X ?? X,
            PreviousY = previous?.Y ?? Y,
            Energy = e.Energy,
            Direction = e.Direction,
            Speed = e.Speed,
            Distance = DistanceTo(e.X, e.Y),
            LastSeenTurn = TurnNumber
        };

        LockRadar();
        MoveAggressively();
        AimAndFire();
        SetRescan();
    }

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        ramDirection *= -1;

        if (target == null)
        {
            TargetSpeed = 8;
            return;
        }

        SetTurnLeft(CalcDeltaAngle(DirectionTo(target.X, target.Y), Direction));
        TargetSpeed = 8;
    }

    public override void OnHitWall(HitWallEvent e)
    {
        ramDirection *= -1;
        TurnTowardArenaCenter();
        TargetSpeed = 8;
    }

    public override void OnHitBot(HitBotEvent e)
    {
        target = new EnemySnapshot
        {
            Id = e.VictimId,
            X = e.X,
            Y = e.Y,
            PreviousX = target?.X ?? e.X,
            PreviousY = target?.Y ?? e.Y,
            Energy = e.Energy,
            Direction = target?.Direction ?? Direction,
            Speed = 0,
            Distance = DistanceTo(e.X, e.Y),
            LastSeenTurn = TurnNumber
        };

        SetTurnLeft(BearingTo(e.X, e.Y));
        TargetSpeed = 8;

        if (GunHeat == 0 && Energy > 1.0)
        {
            SetTurnGunLeft(GunBearingTo(e.X, e.Y));
            SetFire(Math.Min(3.0, Energy - 0.2));
            shotsFired++;
        }
    }

    public override void OnBulletHit(BulletHitBotEvent e)
    {
        shotsHit++;

        if (target != null && target.Id == e.VictimId)
        {
            target.Energy = e.Energy;
        }
    }

    public override void OnBulletHitWall(BulletHitWallEvent e)
    {
        wallMisses++;
    }

    public override void OnBotDeath(BotDeathEvent e)
    {
        if (target?.Id == e.VictimId)
        {
            target = null;
        }
    }

    private void ConfigureIdentity()
    {
        BodyColor = Color.FromArgb(74, 55, 104);
        TurretColor = Color.FromArgb(24, 186, 202);
        RadarColor = Color.FromArgb(16, 25, 48);
        GunColor = Color.FromArgb(220, 223, 228);
        BulletColor = Color.FromArgb(252, 76, 82);
        ScanColor = Color.FromArgb(105, 230, 180);
        TracksColor = Color.FromArgb(42, 43, 48);
    }

    private void ControlRadar()
    {
        RadarTurnRate = MaxRadarTurnRate;

        if (target == null || IsTargetLost())
        {
            SetTurnRadarRight(double.PositiveInfinity);
            return;
        }

        LockRadar();
    }

    private void LockRadar()
    {
        if (target == null)
        {
            return;
        }

        var radarBearing = RadarBearingTo(target.X, target.Y);
        SetTurnRadarLeft(Math.Clamp(radarBearing * RadarLockOvershoot, -MaxRadarTurnRate, MaxRadarTurnRate));
    }

    private void MoveAggressively()
    {
        if (NearWall())
        {
            TurnTowardArenaCenter();
            TargetSpeed = 8;
            return;
        }

        if (target == null || IsTargetLost())
        {
            HuntWhileSearching();
            return;
        }

        var predicted = PredictTargetPosition(1.5);
        var desiredDirection = DirectionTo(predicted.X, predicted.Y);

        if (WouldEndNearWall(desiredDirection, 125))
        {
            desiredDirection = BlendTowardCenter(desiredDirection);
        }

        if (target.Distance < RamDistance)
        {
            desiredDirection = DirectionTo(target.X, target.Y);
        }
        else if (target.Distance < CloseFireDistance)
        {
            desiredDirection = NormalizeAbsoluteAngle(desiredDirection + 12 * ramDirection);
        }

        SetTurnLeft(CalcDeltaAngle(desiredDirection, Direction));
        TargetSpeed = 8;
    }

    private void HuntWhileSearching()
    {
        SetTurnLeft(28 * ramDirection);
        TargetSpeed = 7.5;
    }

    private void AimAndFire()
    {
        if (target == null || IsTargetLost() || GunHeat > 0)
        {
            return;
        }

        var firePower = ChooseFirePower();
        var predicted = PredictTargetPosition(firePower);
        var gunBearing = GunBearingTo(predicted.X, predicted.Y);

        SetTurnGunLeft(gunBearing);

        if (Energy > firePower + 0.2 && Math.Abs(gunBearing) <= AimTolerance(target.Distance))
        {
            SetFire(firePower);
            shotsFired++;
        }
    }

    private double ChooseFirePower()
    {
        if (target == null)
        {
            return 0.1;
        }

        var accuracy = shotsFired == 0 ? 0.45 : (double)shotsHit / shotsFired;
        var power = target.Distance < RamDistance ? 3.0 :
                    target.Distance < CloseFireDistance ? 2.6 :
                    target.Distance < ChaseDistance ? 1.6 :
                    0.85;

        if (target.Energy < 12 && target.Distance < ChaseDistance)
        {
            power = Math.Max(power, Math.Min(2.8, target.Energy / 2.0 + 0.4));
        }

        if (Energy < 16)
        {
            power = Math.Min(power, target.Distance < RamDistance ? 1.4 : 0.8);
        }

        if (target.Distance > CloseFireDistance && (accuracy < 0.25 || wallMisses > shotsHit + 5))
        {
            power = Math.Min(power, 1.15);
        }

        return Math.Clamp(power, 0.2, Math.Min(3.0, Energy - 0.1));
    }

    private PointD PredictTargetPosition(double firePower)
    {
        if (target == null)
        {
            return new PointD(X, Y);
        }

        var bulletSpeed = CalcBulletSpeed(firePower);
        var predictedX = target.X;
        var predictedY = target.Y;
        var radians = target.Direction * Math.PI / 180.0;
        var lateralSpeed = target.Speed;

        for (var turn = 0; turn < 36; turn++)
        {
            var distance = DistanceTo(predictedX, predictedY);

            if (turn * bulletSpeed >= distance)
            {
                break;
            }

            predictedX += Math.Cos(radians) * lateralSpeed;
            predictedY += Math.Sin(radians) * lateralSpeed;

            if (predictedX < PredictionWallMargin ||
                predictedX > ArenaWidth - PredictionWallMargin ||
                predictedY < PredictionWallMargin ||
                predictedY > ArenaHeight - PredictionWallMargin)
            {
                predictedX = Math.Clamp(predictedX, PredictionWallMargin, ArenaWidth - PredictionWallMargin);
                predictedY = Math.Clamp(predictedY, PredictionWallMargin, ArenaHeight - PredictionWallMargin);
                break;
            }
        }

        return new PointD(predictedX, predictedY);
    }

    private double AimTolerance(double distance)
    {
        if (distance < RamDistance)
        {
            return 12;
        }

        if (distance < CloseFireDistance)
        {
            return 8;
        }

        if (distance < ChaseDistance)
        {
            return 4;
        }

        return 2.6;
    }

    private void ForgetLostTarget()
    {
        if (target != null && IsTargetLost())
        {
            target = null;
        }
    }

    private bool IsTargetLost()
    {
        return target != null && TurnNumber - target.LastSeenTurn > LostTargetTurns;
    }

    private bool NearWall()
    {
        return X < WallMargin ||
               X > ArenaWidth - WallMargin ||
               Y < WallMargin ||
               Y > ArenaHeight - WallMargin;
    }

    private bool WouldEndNearWall(double direction, double distance)
    {
        var radians = direction * Math.PI / 180.0;
        var nextX = X + Math.Cos(radians) * distance;
        var nextY = Y + Math.Sin(radians) * distance;

        return nextX < WallMargin ||
               nextX > ArenaWidth - WallMargin ||
               nextY < WallMargin ||
               nextY > ArenaHeight - WallMargin;
    }

    private double BlendTowardCenter(double desiredDirection)
    {
        var centerDirection = DirectionTo(ArenaWidth / 2.0, ArenaHeight / 2.0);
        var delta = CalcDeltaAngle(centerDirection, desiredDirection);
        return NormalizeAbsoluteAngle(desiredDirection + delta * 0.65);
    }

    private void TurnTowardArenaCenter()
    {
        var centerDirection = DirectionTo(ArenaWidth / 2.0, ArenaHeight / 2.0);
        SetTurnLeft(CalcDeltaAngle(centerDirection, Direction));
    }

    private sealed class EnemySnapshot
    {
        public int Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double PreviousX { get; set; }
        public double PreviousY { get; set; }
        public double Energy { get; set; }
        public double Direction { get; set; }
        public double Speed { get; set; }
        public double Distance { get; set; }
        public int LastSeenTurn { get; set; }
    }

    private readonly struct PointD
    {
        public PointD(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }
    }
}
