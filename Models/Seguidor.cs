//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace PARCIALDBEveriflix.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class Seguidor
    {
        public long Id_Seguidor { get; set; }
        public long Id_UsuarioSeguidor { get; set; }
        public long Id_UsuarioSiguiendo { get; set; }
    
        public virtual Usuario Usuario { get; set; }
        public virtual Usuario Usuario1 { get; set; }
    }
}
