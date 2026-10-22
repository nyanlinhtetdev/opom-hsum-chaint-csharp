using System;
using System.Collections.Generic;

namespace HsumChaint.Database.Models;

public partial class MonasteryMember
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public int? MonasterySpaceId { get; set; }

    public int SpaceRoleId { get; set; }

    public bool? IsOwner { get; set; }

    public virtual SpaceRole? SpaceRole { get; set; }

    public virtual User? User { get; set; }

    public virtual MonasterySpace? MonasterySpace { get; set; }
}
