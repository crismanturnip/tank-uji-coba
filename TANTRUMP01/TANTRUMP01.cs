using System;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

public class TANTRUMP01 : Bot
{
    private const double WallMargin = 95;
    private const double PreferredMinDistance = 230;
    private const double PreferredMaxDistance = 470;
    private const int TargetMemoryTurns = 24;

    private readonly Random random = new Random();
    private TargetInfo target;
    private int moveDirection = 1;
    private int lastDirectionChangeTurn;
    private int shotsFired;
    private int shotsHit;
    private int wallMisses;

    static void Main(string[] args)
    {
        new TANTRUMP01().Start();
    }

    TANTRUMP01() : base(BotInfo.FromFile("TANTRUMP01.json")) { }

    public override void Run()
    {
        ConfigureIdentity();

        AdjustGunForBodyTurn = true;
        AdjustRadarForBodyTurn = true;
        AdjustRadarForGunTurn = true;

        MaxSpeed = 7;
        GunTurnRate = 20;
        RadarTurnRate = 45;

        while (IsRunning)
        {
            ForgetStaleTarget();
            SweepRadar();
            MoveByState();
            Go();
        }
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {
        if (!ShouldSwitchTarget(e))
        {
            return;
        }

        var previous = target;
        target = new TargetInfo
        {
            Id = e.ScannedBotId,
            X = e.X,
            Y = e.Y,
            PreviousX = previous != null && previous.Id == e.ScannedBotId ? previous.X : e.X,
            PreviousY = previous != null && previous.Id == e.ScannedBotId ? previous.Y : e.Y,
            Energy = e.Energy,
            Direction = e.Direction,
            Speed = e.Speed,
            Distance = DistanceTo(e.X, e.Y),
            LastSeenTurn = TurnNumber
        };

        LockRadar();
        AimAndFire();
        MoveByState();
        SetRescan();
    }

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        ChangeMoveDirection();
        MaxSpeed = Energy < 25 ? 8 : 7;
    }

    public override void OnHitWall(HitWallEvent e)
    {
        ChangeMoveDirection();
        TurnAwayFromWall();
        TargetSpeed = 6 * moveDirection;
    }

    public override void OnHitBot(HitBotEvent e)
    {
        var bearing = BearingTo(e.X, e.Y);

        if (Math.Abs(bearing) < 35 && GunHeat == 0)
        {
            SetTurnGunLeft(GunBearingTo(e.X, e.Y));
            SetFire(Math.Min(2.4, Math.Max(0.5, Energy - 0.1)));
            shotsFired++;
        }

        ChangeMoveDirection();
        TargetSpeed = e.IsRammed ? -6 : 5;
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
        if (target != null && target.Id == e.VictimId)
        {
            target = null;
        }
    }

    private void ConfigureIdentity()
    {
        BodyColor = Color.FromArgb(42, 47, 52);
        TurretColor = Color.FromArgb(215, 64, 45);
        RadarColor = Color.FromArgb(255, 196, 87);
        GunColor = Color.FromArgb(245, 245, 245);
        BulletColor = Color.FromArgb(255, 196, 87);
        ScanColor = Color.FromArgb(44, 190, 160);
        TracksColor = Color.FromArgb(20, 20, 20);
    }

    private void SweepRadar()
    {
        if (target == null)
        {
            SetTurnRadarRight(360);
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
        SetTurnRadarLeft(radarBearing * 1.8);
    }

    private void AimAndFire()
    {
        if (target == null)
        {
            return;
        }

        var firePower = ChooseFirePower(target.Distance, target.Energy);
        var predicted = PredictTargetPosition(firePower);
        var gunBearing = GunBearingTo(predicted.X, predicted.Y);

        SetTurnGunLeft(gunBearing);

        if (GunHeat == 0 && Math.Abs(gunBearing) <= AimTolerance(target.Distance) && Energy > firePower + 0.2)
        {
            SetFire(firePower);
            shotsFired++;
        }
    }

    private double ChooseFirePower(double distance, double enemyEnergy)
    {
        var accuracy = shotsFired == 0 ? 0.55 : (double)shotsHit / shotsFired;
        var power = distance < 150 ? 2.8 : distance < 400 ? 1.8 : 1.0;

        if (enemyEnergy < 8)
        {
            power = Math.Min(power, Math.Max(0.5, enemyEnergy / 4));
        }

        if (Energy < 20)
        {
            power = Math.Min(power, 1.0);
        }

        if (accuracy < 0.28 || wallMisses > shotsHit + 4)
        {
            power = Math.Min(power, 1.2);
        }

        return Math.Clamp(power, 0.5, Math.Min(3.0, Energy - 0.1));
    }

    private PointD PredictTargetPosition(double firePower)
    {
        if (target == null || target.Distance < 160)
        {
            return new PointD(target?.X ?? X, target?.Y ?? Y);
        }

        var bulletSpeed = CalcBulletSpeed(firePower);
        var travelTurns = Math.Min(28, target.Distance / bulletSpeed);
        var radians = target.Direction * Math.PI / 180;
        var predictedX = target.X + Math.Cos(radians) * target.Speed * travelTurns;
        var predictedY = target.Y + Math.Sin(radians) * target.Speed * travelTurns;

        return new PointD(
            Math.Clamp(predictedX, WallMargin, ArenaWidth - WallMargin),
            Math.Clamp(predictedY, WallMargin, ArenaHeight - WallMargin)
        );
    }

    private double AimTolerance(double distance)
    {
        if (distance < 160)
        {
            return 7;
        }

        if (distance < 420)
        {
            return 4;
        }

        return 2.5;
    }

    private void MoveByState()
    {
        if (NearWall())
        {
            TurnAwayFromWall();
            TargetSpeed = 6 * moveDirection;
            return;
        }

        if (target == null)
        {
            SetTurnRight(35 * moveDirection);
            TargetSpeed = 5;
            return;
        }

        if (TurnNumber - lastDirectionChangeTurn > 38 + random.Next(18))
        {
            ChangeMoveDirection();
        }

        var targetDirection = DirectionTo(target.X, target.Y);
        var desiredDirection = targetDirection + 90 * moveDirection;

        if (target.Distance < PreferredMinDistance)
        {
            desiredDirection = targetDirection + 150 * moveDirection;
        }
        else if (target.Distance > PreferredMaxDistance)
        {
            desiredDirection = targetDirection + 45 * moveDirection;
        }

        SetTurnLeft(CalcDeltaAngle(desiredDirection, Direction));
        TargetSpeed = Energy < 18 ? 8 * moveDirection : 6.5 * moveDirection;
    }

    private bool ShouldSwitchTarget(ScannedBotEvent e)
    {
        if (target == null || target.Id == e.ScannedBotId)
        {
            return true;
        }

        var currentScore = TargetScore(target.Distance, target.Energy, target.LastSeenTurn);
        var candidateDistance = DistanceTo(e.X, e.Y);
        var candidateScore = TargetScore(candidateDistance, e.Energy, TurnNumber);

        return candidateScore > currentScore + 20;
    }

    private double TargetScore(double distance, double energy, int lastSeenTurn)
    {
        var distanceScore = Math.Max(0, 520 - distance);
        var weakBonus = Math.Max(0, 35 - energy) * 4;
        var stalePenalty = Math.Max(0, TurnNumber - lastSeenTurn) * 8;

        return distanceScore + weakBonus - stalePenalty;
    }

    private void ForgetStaleTarget()
    {
        if (target != null && TurnNumber - target.LastSeenTurn > TargetMemoryTurns)
        {
            target = null;
        }
    }

    private bool NearWall()
    {
        return X < WallMargin || X > ArenaWidth - WallMargin || Y < WallMargin || Y > ArenaHeight - WallMargin;
    }

    private void TurnAwayFromWall()
    {
        var centerDirection = DirectionTo(ArenaWidth / 2.0, ArenaHeight / 2.0);
        SetTurnLeft(CalcDeltaAngle(centerDirection, Direction));
    }

    private void ChangeMoveDirection()
    {
        moveDirection *= -1;
        lastDirectionChangeTurn = TurnNumber;
    }

    private sealed class TargetInfo
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
