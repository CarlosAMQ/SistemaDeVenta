using SistemaVenta.BLL.Interfaces;
using SistemaVenta.DAL.Interfaces;
using SistemaVenta.Entity;
using SistemaVenta.Entity.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace SistemaVenta.BLL.Implementacion
{
    public class NegocioService : INegocioService
    {
        private readonly IGenericRepository<Negocio> _repositorio;
        private readonly ILocalStorageService _cloudinaryServices;

        public NegocioService(IGenericRepository<Negocio> repositorio, ILocalStorageService cloudinaryServices)
        {
            _repositorio = repositorio;
            _cloudinaryServices = cloudinaryServices;
        }

        public async Task<Negocio> Obtener()
        {
            try
            {
                Negocio negocio_encontrado = await _repositorio.Obtener(n => n.IdNegocio == 1);
                return negocio_encontrado;
            }
            catch
            {
                throw;
            }
        }

        public async Task<Negocio> GuardarCambios(Negocio entidad, Stream Logo = null, string NombreLogo = "")
        {
            try
            {
                Negocio negocio_encontrado = await _repositorio.Obtener(n => n.IdNegocio == 1);

                negocio_encontrado.Rfc = entidad.Rfc;
                negocio_encontrado.Nombre = entidad.Nombre;
                negocio_encontrado.Correo = entidad.Correo;
                negocio_encontrado.Direccion = entidad.Direccion;
                negocio_encontrado.Telefono = entidad.Telefono;
                negocio_encontrado.CodigoPostal = entidad.CodigoPostal;
                negocio_encontrado.SimboloMoneda = entidad.SimboloMoneda;
                negocio_encontrado.RegimenFiscal = entidad.RegimenFiscal;

                negocio_encontrado.NombreLogo = negocio_encontrado.NombreLogo == "" ? NombreLogo : negocio_encontrado.NombreLogo;

                if (Logo != null && Logo.Length > 0)
                {
                    string nombreLogo = Guid.NewGuid().ToString("N") + ".JPG";
                    string urlLogo = await _cloudinaryServices.SubirStorage(Logo, "carpeta_logo", nombreLogo);

                    negocio_encontrado.NombreLogo = nombreLogo;
                    negocio_encontrado.UrlLogo = urlLogo;

                    Console.WriteLine("URL generada: " + urlLogo);
                }

                await _repositorio.Editar(negocio_encontrado);
                return negocio_encontrado;

            }
            catch
            {
                throw;
            }
        }

    }
}
